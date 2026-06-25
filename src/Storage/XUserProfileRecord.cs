namespace Storage;

public class XUserProfileRecord
{
    public Guid Id { get; set; }
    public string XUserId { get; set; } = default!;
    public string? ScrapedUsername { get; set; }
    public string? CustomName { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public UserRecord? CreatedByUser { get; set; }
    public UserRecord? UpdatedByUser { get; set; }
}
