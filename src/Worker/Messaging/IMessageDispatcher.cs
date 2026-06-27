using Application.Models;

namespace Worker.Messaging;

public interface IMessageDispatcher
{
    Task<bool> DispatchAsync(QueueMessageEnvelope message, CancellationToken ct);
}
