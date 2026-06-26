using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public partial class TweetSubmissionService : ITweetSubmissionService
{
    private readonly IAppDbContext _db;
    private readonly IScrapeQueueService _queue;
    private readonly ILogger<TweetSubmissionService> _logger;

    public TweetSubmissionService(IAppDbContext db, IScrapeQueueService queue, ILogger<TweetSubmissionService> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    public async Task<Result<TweetSubmissionResult>> SubmitAsync(SubmitTweetCommand command, CancellationToken ct)
    {
        var match = TweetUrlRegex().Match(command.TweetUrl);
        if (!match.Success)
            return Result.Failure<TweetSubmissionResult>(DomainError.Validation("Invalid tweet URL. Expected format: https://x.com/{username}/status/{id}"));

        var authorXUsername = match.Groups[1].Value;
        var xTweetId = match.Groups[2].Value;

        var existing = await _db.Tweets.FirstOrDefaultAsync(t => t.XTweetId == xTweetId, ct);
        if (existing != null)
        {
            return Result.Failure<TweetSubmissionResult>(DomainError.Conflict("Already submitted"));
        }

        var correlationId = Guid.NewGuid().ToString();
        var tweetId = Guid.NewGuid();

        var auditPayload = JsonSerializer.Serialize(new
        {
            xTweetUrl = command.TweetUrl,
            xTweetId,
            authorXUsername,
            folderIds = command.FolderIds,
            submittedByUserId = command.SubmittedByUserId,
            submittedByIp = command.SubmittedByIp
        });

        _db.AuditLogs.Add(new AuditLogRecord
        {
            CorrelationId = correlationId,
            Action = "Tweet.SubmitRequest",
            EntityType = "Tweet",
            EntityId = xTweetId,
            PerformedByUserId = command.SubmittedByUserId,
            IpAddress = command.SubmittedByIp,
            Payload = auditPayload
        });

        _db.Tweets.Add(new TweetRecord
        {
            Id = tweetId,
            XTweetId = xTweetId,
            XTweetUrl = command.TweetUrl,
            AuthorXUsername = authorXUsername,
            FetchStatus = "Pending",
            SubmittedByUserId = command.SubmittedByUserId,
            SubmittedByIp = command.SubmittedByIp
        });

        await _db.SaveChangesAsync(ct);

        try
        {
            await _queue.EnqueueAsync(new ScrapeJobMessage(
                XTweetUrl: command.TweetUrl,
                XTweetId: xTweetId,
                AuthorXUsername: authorXUsername,
                FolderIds: command.FolderIds,
                SubmittedByUserId: command.SubmittedByUserId,
                SubmittedByIp: command.SubmittedByIp,
                CorrelationId: correlationId), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue scrape job for tweet {XTweetId}. Audit log recorded with CorrelationId {CorrelationId}", xTweetId, correlationId);
        }

        _logger.LogInformation("Tweet submitted: {TweetId}, XTweetId: {XTweetId}, CorrelationId: {CorrelationId}", tweetId, xTweetId, correlationId);

        return Result.Success(new TweetSubmissionResult(tweetId, "Pending"));
    }

    [GeneratedRegex(@"https?://(?:x|twitter)\.com/([^/]+)/status/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TweetUrlRegex();
}
