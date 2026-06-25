using Domain.Entities;
using Domain.Enums;
using Storage;

namespace Domain.Mappers;

public static class TweetMapper
{
    public static Tweet ToDomain(TweetRecord record)
    {
        return new Tweet
        {
            Id = record.Id,
            XTweetId = record.XTweetId,
            XTweetUrl = record.XTweetUrl,
            AuthorXUserId = record.AuthorXUserId,
            AuthorXUsername = record.AuthorXUsername,
            TweetText = record.TweetText,
            TweetDate = record.TweetDate,
            ScreenshotBlobName = record.ScreenshotBlobName,
            Tags = record.Tags,
            VoteCount = record.VoteCount,
            FetchStatus = Enum.Parse<FetchStatus>(record.FetchStatus),
            ScrapeAttempts = record.ScrapeAttempts,
            ScrapeError = record.ScrapeError,
            SubmittedByUserId = record.SubmittedByUserId,
            SubmittedByIp = record.SubmittedByIp,
            CreatedAt = record.CreatedAt,
            ScrapedAt = record.ScrapedAt,
            SubmittedByUser = record.SubmittedByUser is not null ? UserMapper.ToDomain(record.SubmittedByUser) : null,
            FolderTweets = record.FolderTweets.Select(FolderTweetMapper.ToDomain).ToList(),
            Votes = record.Votes.Select(VoteMapper.ToDomain).ToList(),
        };
    }

    public static TweetRecord ToRecord(Tweet entity)
    {
        return new TweetRecord
        {
            Id = entity.Id,
            XTweetId = entity.XTweetId,
            XTweetUrl = entity.XTweetUrl,
            AuthorXUserId = entity.AuthorXUserId,
            AuthorXUsername = entity.AuthorXUsername,
            TweetText = entity.TweetText,
            TweetDate = entity.TweetDate,
            ScreenshotBlobName = entity.ScreenshotBlobName,
            Tags = entity.Tags,
            VoteCount = entity.VoteCount,
            FetchStatus = entity.FetchStatus.ToString(),
            ScrapeAttempts = entity.ScrapeAttempts,
            ScrapeError = entity.ScrapeError,
            SubmittedByUserId = entity.SubmittedByUserId,
            SubmittedByIp = entity.SubmittedByIp,
            CreatedAt = entity.CreatedAt,
            ScrapedAt = entity.ScrapedAt,
        };
    }
}
