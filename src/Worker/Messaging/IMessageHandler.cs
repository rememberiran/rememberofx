using Application.Models;

namespace Worker.Messaging;

public interface IMessageHandler
{
    string MessageType { get; }

    Task<bool> HandleAsync(QueueMessageEnvelope message, CancellationToken ct);
}
