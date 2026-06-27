using Application.Models;

namespace Worker.Messaging;

public interface IMessageParser
{
    QueueMessageEnvelope? TryParse(string base64Message);
}
