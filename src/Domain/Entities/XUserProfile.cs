namespace Domain.Entities;

public class XUserProfile
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

    public User? CreatedByUser { get; set; }
    public User? UpdatedByUser { get; set; }
}
