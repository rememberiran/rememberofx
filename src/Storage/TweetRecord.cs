namespace Storage;

public class TweetRecord
{
    public Guid Id { get; set; }
    public string XTweetId { get; set; } = default!;
    public string XTweetUrl { get; set; } = default!;
    public string? AuthorXUserId { get; set; }
    public string? AuthorXUsername { get; set; }
    public string? TweetText { get; set; }
    public DateTime? TweetDate { get; set; }
    public string? ScreenshotBlobName { get; set; }
    public string? Tags { get; set; }
    public int VoteCount { get; set; }
    public string FetchStatus { get; set; } = $"Pending";
    public int ScrapeAttempts { get; set; }
    public string? ScrapeError { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public string SubmittedByIp { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScrapedAt { get; set; }
    public bool IsAnonymous { get; set; }

    public UserRecord? SubmittedByUser { get; set; }
    public ICollection<TweetMediaRecord> Media { get; set; } = new List<TweetMediaRecord>();
    public ICollection<FolderTweetRecord> FolderTweets { get; set; } = new List<FolderTweetRecord>();
    public ICollection<VoteRecord> Votes { get; set; } = new List<VoteRecord>();
}
