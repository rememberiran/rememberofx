using System.Text.Json;
using Application;
using Application.Interfaces;
using Application.Models;
using Ingestion.Twitter;
using Microsoft.EntityFrameworkCore;
using Storage;
using Worker.Messaging;

namespace Worker.Handlers;

public class ScrapeTweetHandler : IMessageHandler
{
    private const int MaxDequeueCount = 3;

    private static readonly EventId ScrapeStartedEvent = new(4001, "ScrapeStarted");
    private static readonly EventId ScrapeCompletedEvent = new(4002, "ScrapeCompleted");
    private static readonly EventId ScrapeFailedEvent = new(4003, "ScrapeFailed");
    private static readonly EventId DuplicateDetectedEvent = new(4004, "DuplicateDetected");
    private static readonly EventId MaxRetriesEvent = new(4005, "MaxRetriesExceeded");
    private static readonly EventId ScreenshotUploadFailedEvent = new(4006, "ScreenshotUploadFailed");
    private static readonly EventId MediaUploadFailedEvent = new(4007, "MediaUploadFailed");

    private readonly IAppDbContext _db;
    private readonly ITweetScraper _scraper;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ScrapeTweetHandler> _logger;

    public ScrapeTweetHandler(
        IAppDbContext db,
        ITweetScraper scraper,
        IBlobStorageService blobStorage,
        ILogger<ScrapeTweetHandler> logger)
    {
        _db = db;
        _scraper = scraper;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public string MessageType => "ScrapeTweet";

    public async Task<bool> HandleAsync(QueueMessageEnvelope message, CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<ScrapeJobMessage>(message.RawBody)!;

        _logger.LogInformation(ScrapeStartedEvent, "Processing scrape job for {XTweetUrl}", job.XTweetUrl);

        var existing = await _db.Tweets.AsNoTracking().AnyAsync(t => t.XTweetId == job.XTweetId, ct);
        if (existing)
        {
            _logger.LogInformation(DuplicateDetectedEvent, "Duplicate — tweet already processed");
            return true;
        }

        if (message.DequeueCount >= MaxDequeueCount)
        {
            _logger.LogWarning(MaxRetriesEvent, "Max retries exceeded after {DequeueCount} attempts", message.DequeueCount);
            await PersistFailureAsync(message, job, $"Max retries exceeded", ct);
            return true;
        }

        try
        {
            var scraped = await _scraper.ScrapeAsync(job.XTweetUrl, ct);

            if (string.IsNullOrEmpty(scraped.Text))
            {
                _logger.LogWarning(ScrapeFailedEvent, "Tweet content unavailable — may be deleted or private");
                await PersistFailureAsync(message, job, $"Tweet content unavailable", ct);
                return true;
            }

            var screenshotBlobName = await UploadScreenshotAsync(scraped, job.XTweetId, ct);
            var mediaRecords = await UploadAndCreateMediaRecordsAsync(scraped, job.XTweetId, ct);

            await PersistSuccessAsync(message, job, scraped, screenshotBlobName, mediaRecords, ct);

            _logger.LogInformation(ScrapeCompletedEvent, "Tweet scraped and persisted successfully");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ScrapeFailedEvent, ex, "Failed to process scrape job — message will retry");
            return false;
        }
    }

    private async Task<string?> UploadScreenshotAsync(ScrapedTweet scraped, string xTweetId, CancellationToken ct)
    {
        if (scraped.Screenshot is null)
        {
            return null;
        }

        try
        {
            var blobName = $"{xTweetId}.png";
            await _blobStorage.UploadScreenshotAsync(blobName, scraped.Screenshot.Data, scraped.Screenshot.ContentType, ct);
            return blobName;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ScreenshotUploadFailedEvent, ex, "Failed to upload screenshot — continuing without it");
            return null;
        }
    }

    private async Task<List<TweetMediaRecord>> UploadAndCreateMediaRecordsAsync(ScrapedTweet scraped, string xTweetId, CancellationToken ct)
    {
        var records = new List<TweetMediaRecord>();
        var index = 0;

        foreach (var media in scraped.Media)
        {
            string? blobName = null;

            try
            {
                var extension = media.MediaType == ScrapedMediaType.Video ? ".mp4" : GetExtensionFromContentType(media.ContentType);
                blobName = $"{xTweetId}_{index}{extension}";
                await _blobStorage.UploadMediaAsync(blobName, media.Data, media.ContentType, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(MediaUploadFailedEvent, ex, "Failed to upload media {Index} — recording without blob", index);
                blobName = null;
            }

            records.Add(new TweetMediaRecord
            {
                Id = Guid.NewGuid(),
                MediaType = media.MediaType == ScrapedMediaType.Video ? "Video" : "Image",
                BlobName = blobName,
                OriginalUrl = null,
                OrderIndex = index,
            });

            index++;
        }

        return records;
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            _ => ".jpg",
        };
    }

    private async Task PersistSuccessAsync(
        QueueMessageEnvelope envelope,
        ScrapeJobMessage job,
        ScrapedTweet scraped,
        string? screenshotBlobName,
        List<TweetMediaRecord> mediaRecords,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var tweetId = Guid.NewGuid();
        var tags = ExtractHashtags(scraped.Text);

        var tweet = new TweetRecord
        {
            Id = tweetId,
            XTweetId = job.XTweetId,
            XTweetUrl = job.XTweetUrl,
            AuthorXUserId = scraped.UserId,
            AuthorXUsername = scraped.UserHandle,
            TweetText = scraped.Text,
            TweetDate = scraped.Date,
            ScreenshotBlobName = screenshotBlobName,
            Tags = tags,
            FetchStatus = "Ok",
            ScrapeAttempts = 1,
            SubmittedByUserId = envelope.UserId,
            SubmittedByIp = envelope.IpAddress,
            ScrapedAt = DateTime.UtcNow,
        };

        _db.Tweets.Add(tweet);

        foreach (var mediaRecord in mediaRecords)
        {
            mediaRecord.TweetId = tweetId;
            _db.TweetMedia.Add(mediaRecord);
        }

        UpsertXUserProfile(scraped);

        if (job.FolderIds is not null)
        {
            foreach (var folderId in job.FolderIds)
            {
                _db.FolderTweets.Add(new FolderTweetRecord
                {
                    FolderId = folderId,
                    TweetId = tweetId,
                    AddedByUserId = envelope.UserId ?? Guid.Empty,
                });
            }
        }

        _db.AuditLogs.Add(new AuditLogRecord
        {
            CorrelationId = envelope.CorrelationId,
            Action = "Tweet.Scraped",
            EntityType = "Tweet",
            EntityId = job.XTweetId,
            PerformedByUserId = envelope.UserId,
            IpAddress = envelope.IpAddress,
        });

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task PersistFailureAsync(QueueMessageEnvelope envelope, ScrapeJobMessage job, string error, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _db.Tweets.Add(new TweetRecord
        {
            Id = Guid.NewGuid(),
            XTweetId = job.XTweetId,
            XTweetUrl = job.XTweetUrl,
            AuthorXUsername = job.AuthorXUsername,
            FetchStatus = "ScrapeFailed",
            ScrapeError = error,
            ScrapeAttempts = MaxDequeueCount,
            SubmittedByUserId = envelope.UserId,
            SubmittedByIp = envelope.IpAddress,
        });

        _db.AuditLogs.Add(new AuditLogRecord
        {
            CorrelationId = envelope.CorrelationId,
            Action = "Tweet.ScrapeFailed",
            EntityType = "Tweet",
            EntityId = job.XTweetId,
            PerformedByUserId = envelope.UserId,
            IpAddress = envelope.IpAddress,
        });

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void UpsertXUserProfile(ScrapedTweet scraped)
    {
        if (string.IsNullOrEmpty(scraped.UserId))
        {
            return;
        }

        var existing = _db.XUserProfiles.Local.FirstOrDefault(p => string.Equals(p.XUserId, scraped.UserId, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.XUsername = scraped.UserHandle;
            return;
        }

        _db.XUserProfiles.Add(new XUserProfileRecord
        {
            Id = Guid.NewGuid(),
            XUserId = scraped.UserId,
            XUsername = scraped.UserHandle,
        });
    }

    private static string? ExtractHashtags(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var tags = new List<string>();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith('#') && word.Length > 1)
            {
                tags.Add(word.TrimStart('#'));
            }
        }

        return tags.Count > 0
            ? JsonSerializer.Serialize(tags)
            : null;
    }
}
