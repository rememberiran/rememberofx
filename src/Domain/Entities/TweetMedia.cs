using Domain.Enums;

namespace Domain.Entities;

public class TweetMedia
{
    public Guid Id { get; set; }
    public Guid TweetId { get; set; }
    public MediaType MediaType { get; set; }
    public string? BlobName { get; set; }
    public string? OriginalUrl { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
