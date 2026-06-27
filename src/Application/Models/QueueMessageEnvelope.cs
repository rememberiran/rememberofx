namespace Application.Models;

public record QueueMessageEnvelope(
    string MessageId,
    string PopReceipt,
    long DequeueCount,
    string MessageType,
    string RawBody,
    string CorrelationId,
    Guid? UserId,
    string IpAddress);
