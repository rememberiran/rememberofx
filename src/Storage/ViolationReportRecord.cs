namespace Storage;

public class ViolationReportRecord
{
    public Guid Id { get; set; }
    public Guid ReportedUserId { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public string ReportedByIp { get; set; } = default!;
    public string Explanation { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending";
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public UserRecord ReportedUser { get; set; } = default!;
    public UserRecord? ReportedByUser { get; set; }
    public UserRecord? ReviewedByUser { get; set; }
}
