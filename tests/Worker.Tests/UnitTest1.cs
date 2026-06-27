using Application.Interfaces;
using Application.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Worker.Messaging;

namespace Worker.Tests;

public class ScrapeWorkerTests
{
    [Fact]
    public void ScrapeWorkerCanBeConstructed()
    {
        using var worker = new ScrapeWorker(
            new FakeQueueService(),
            new FakeParser(),
            new FakeDispatcher(),
            NullLogger<ScrapeWorker>.Instance);

        Assert.NotNull(worker);
    }

    private sealed class FakeQueueService : IQueueService
    {
        public Task EnqueueAsync<T>(T message, CancellationToken ct) => Task.CompletedTask;
        public Task<RawQueueMessage?> DequeueAsync(TimeSpan visibilityTimeout, CancellationToken ct) => Task.FromResult<RawQueueMessage?>(null);
        public Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> IsHealthyAsync(CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeParser : IMessageParser
    {
        public QueueMessageEnvelope? TryParse(string base64Message) => null;
    }

    private sealed class FakeDispatcher : IMessageDispatcher
    {
        public Task<bool> DispatchAsync(QueueMessageEnvelope message, CancellationToken ct) => Task.FromResult(true);
    }
}
