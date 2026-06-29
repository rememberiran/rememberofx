namespace Storage;

public class TweetMediaRecord
{
    public Guid Id { get; set; }
    public Guid TweetId { get; set; }
    public string MediaType { get; set; } = default!;
    public string? BlobName { get; set; }
    public string? OriginalUrl { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TweetRecord Tweet { get; set; } = default!;
}
