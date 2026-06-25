using Domain.Enums;

namespace Domain.Entities;

public class Tweet
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
    public FetchStatus FetchStatus { get; set; } = FetchStatus.Pending;
    public int ScrapeAttempts { get; set; }
    public string? ScrapeError { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public string SubmittedByIp { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScrapedAt { get; set; }

    public User? SubmittedByUser { get; set; }
    public ICollection<FolderTweet> FolderTweets { get; set; } = new List<FolderTweet>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
