using System.Text;
using System.Text.Json;
using Application.Models;

namespace Worker.Messaging;

public class MessageParser : IMessageParser
{
    private static readonly EventId ParseFailedEvent = new(4030, "MessageParseFailed");

    private readonly ILogger<MessageParser> _logger;

    public MessageParser(ILogger<MessageParser> logger)
    {
        _logger = logger;
    }

    public QueueMessageEnvelope? TryParse(string base64Message)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Message));
            return JsonSerializer.Deserialize<QueueMessageEnvelope>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ParseFailedEvent, ex, "Failed to parse queue message");
            return null;
        }
    }
}
