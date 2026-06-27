namespace Application.Interfaces;

public interface IQueueService
{
    Task EnqueueAsync<T>(T message, CancellationToken ct);
    Task<RawQueueMessage?> DequeueAsync(TimeSpan visibilityTimeout, CancellationToken ct);
    Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct);
    Task<bool> IsHealthyAsync(CancellationToken ct);
}

public record RawQueueMessage(string Body, string MessageId, string PopReceipt, long DequeueCount);
