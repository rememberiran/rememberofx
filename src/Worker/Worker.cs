using Application.Interfaces;
using Application.Models;
using Worker.Messaging;

namespace Worker;

public class ScrapeWorker : BackgroundService
{
    private static readonly EventId WorkerStartedEvent = new(4020, "WorkerStarted");
    private static readonly EventId MessageReceivedEvent = new(4021, "MessageReceived");
    private static readonly EventId MessageProcessedEvent = new(4022, "MessageProcessed");
    private static readonly EventId MessageFailedEvent = new(4023, "MessageFailed");
    private static readonly EventId MessageUnparseableEvent = new(4024, "MessageUnparseable");
    private static readonly EventId IdlePollEvent = new(4025, "IdlePoll");

    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly IQueueService _queue;
    private readonly IMessageParser _parser;
    private readonly IMessageDispatcher _dispatcher;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly ILogger<ScrapeWorker> _logger;
    private readonly int _maxConcurrent;

    public ScrapeWorker(
        IQueueService queue,
        IMessageParser parser,
        IMessageDispatcher dispatcher,
        ILogger<ScrapeWorker> logger,
        int maxConcurrent = 2)
    {
        _queue = queue;
        _parser = parser;
        _dispatcher = dispatcher;
        _logger = logger;
        _maxConcurrent = maxConcurrent;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public override void Dispose()
    {
        _concurrencyLimiter.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(WorkerStartedEvent, "Scrape Worker started (max concurrent: {MaxConcurrent})", _maxConcurrent);

        var consecutiveEmpty = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            var response = await _queue.DequeueAsync(VisibilityTimeout, stoppingToken);

            if (response is null)
            {
                _concurrencyLimiter.Release();
                consecutiveEmpty++;

                var backoff = CalculateBackoff(consecutiveEmpty);
                _logger.LogDebug(IdlePollEvent, "No messages, backing off {BackoffMs}ms", backoff.TotalMilliseconds);
                await Task.Delay(backoff, stoppingToken);
                continue;
            }

            consecutiveEmpty = 0;

            var envelope = _parser.TryParse(response.Body.ToString()!);

            if (envelope is null)
            {
                _concurrencyLimiter.Release();
                _logger.LogWarning(MessageUnparseableEvent, "Unparseable message {MessageId} — deleting", response.MessageId);
                await _queue.DeleteMessageAsync(response.MessageId, response.PopReceipt, stoppingToken);
                continue;
            }

            var enrichedEnvelope = envelope with
            {
                MessageId = response.MessageId,
                PopReceipt = response.PopReceipt,
                DequeueCount = response.DequeueCount,
            };

            _ = ProcessInBackgroundAsync(enrichedEnvelope, stoppingToken);
        }
    }

    private async Task ProcessInBackgroundAsync(QueueMessageEnvelope envelope, CancellationToken ct)
    {
        try
        {
            using (_logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["CorrelationId"] = envelope.CorrelationId,
                ["UserId"] = envelope.UserId,
                ["IpAddress"] = envelope.IpAddress,
            }))
            {
                _logger.LogInformation(
                    MessageReceivedEvent,
                    "Processing message {MessageId} (type: {MessageType}, attempt: {DequeueCount})",
                    envelope.MessageId,
                    envelope.MessageType,
                    envelope.DequeueCount);

                var success = await _dispatcher.DispatchAsync(envelope, ct);

                if (success)
                {
                    await _queue.DeleteMessageAsync(envelope.MessageId, envelope.PopReceipt, ct);
                    _logger.LogInformation(MessageProcessedEvent, "Message {MessageId} processed successfully", envelope.MessageId);
                }
                else
                {
                    _logger.LogWarning(MessageFailedEvent, "Message {MessageId} processing failed — will retry via visibility timeout", envelope.MessageId);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(MessageFailedEvent, ex, "Unhandled error processing message {MessageId}", envelope.MessageId);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private static TimeSpan CalculateBackoff(int consecutiveEmpty)
    {
        var seconds = Math.Min(MinBackoff.TotalSeconds * Math.Pow(2, consecutiveEmpty - 1), MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
