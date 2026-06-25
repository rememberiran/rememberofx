namespace Storage;

public class AuditLogRecord
{
    public long Id { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string? EntityId { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string IpAddress { get; set; } = default!;
    public string? Region { get; set; }
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserRecord? PerformedByUser { get; set; }
}
