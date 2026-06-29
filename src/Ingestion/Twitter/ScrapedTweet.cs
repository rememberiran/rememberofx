namespace Ingestion.Twitter;

public class ScrapedTweet
{
    public string TweetUrl { get; set; } = default!;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserHandle { get; set; }
    public string? Text { get; set; }
    public DateTime? Date { get; set; }
    public ScrapedMedia? Screenshot { get; set; }
    public ICollection<ScrapedMedia> Media { get; set; } = new List<ScrapedMedia>();
}

public class ScrapedMedia
{
    public ReadOnlyMemory<byte> Data { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public ScrapedMediaType MediaType { get; set; }
}

public enum ScrapedMediaType
{
    Image,
    Video,
}
