using System.Text.Json;
using Application;
using Application.Interfaces;
using Application.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Queue;

public class QueueService : IQueueService
{
    private static readonly EventId EnqueuedEvent = new(3001, "MessageEnqueued");

    private readonly QueueClient _queueClient;
    private readonly IAsyncContext<CorrelationContext> _correlationContext;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly ILogger<QueueService> _logger;

    public QueueService(
        IConfiguration configuration,
        ITokenCredentialProvider credentialProvider,
        IAsyncContext<CorrelationContext> correlationContext,
        IAsyncContext<IdentityContext> identityContext,
        ILogger<QueueService> logger)
    {
        var accountUrl = configuration[$"Queue:AccountUrl"];
        var queueName = configuration[$"Queue:QueueName"] ?? $"scrape-jobs";

        _queueClient = new QueueClient(new Uri($"{accountUrl}{queueName}"), credentialProvider.Credential);
        _correlationContext = correlationContext;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task EnqueueAsync<T>(T message, CancellationToken ct)
    {
        var envelope = new QueueMessageEnvelope(
            MessageId: string.Empty,
            PopReceipt: string.Empty,
            DequeueCount: 0,
            MessageType: typeof(T).Name,
            RawBody: JsonSerializer.Serialize(message),
            CorrelationId: _correlationContext.Value?.CorrelationId ?? string.Empty,
            UserId: _identityContext.Value?.InternalUserId,
            IpAddress: _identityContext.Value?.IpAddress ?? string.Empty);

        var json = JsonSerializer.Serialize(envelope);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        await _queueClient.SendMessageAsync(base64, cancellationToken: ct);
        _logger.LogInformation(EnqueuedEvent, "Enqueued message of type {MessageType}", typeof(T).Name);
    }

    public async Task<RawQueueMessage?> DequeueAsync(TimeSpan visibilityTimeout, CancellationToken ct)
    {
        var response = await _queueClient.ReceiveMessageAsync(visibilityTimeout, ct);
        if (response.Value is null)
        {
            return null;
        }

        var msg = response.Value;
        return new RawQueueMessage(msg.Body.ToString(), msg.MessageId, msg.PopReceipt, msg.DequeueCount);
    }

    public async Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        await _queueClient.DeleteMessageAsync(messageId, popReceipt, ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        var properties = await _queueClient.GetPropertiesAsync(ct);
        return properties.HasValue;
    }
}
