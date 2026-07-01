namespace Domain.Entities;

public class ViolationReport
{
    public Guid Id { get; set; }
    public Guid ReportedUserId { get; set; }
    public string ReportedXUsername { get; set; } = default!;
    public Guid? ReportedByUserId { get; set; }
    public string ReportedByIp { get; set; } = default!;
    public string Explanation { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = default!;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}
