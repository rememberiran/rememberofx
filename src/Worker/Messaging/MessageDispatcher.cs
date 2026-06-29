using Application.Models;

namespace Worker.Messaging;

public class MessageDispatcher : IMessageDispatcher
{
    private static readonly EventId UnknownMessageTypeEvent = new(4010, "UnknownMessageType");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(IServiceScopeFactory scopeFactory, ILogger<MessageDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> DispatchAsync(QueueMessageEnvelope message, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IMessageHandler>();

        var handler = handlers.FirstOrDefault(h => string.Equals(h.MessageType, message.MessageType, StringComparison.Ordinal));

        if (handler is null)
        {
            _logger.LogWarning(UnknownMessageTypeEvent, "No handler registered for message type: {MessageType}", message.MessageType);
            return false;
        }

        return await handler.HandleAsync(message, ct);
    }
}
