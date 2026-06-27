using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public partial class TweetSubmissionService : ITweetSubmissionService
{
    private readonly IAppDbContext _db;
    private readonly IQueueService _queue;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private static readonly EventId TweetSubmittedEvent = new(1050, "TweetSubmitted");
    private static readonly EventId EnqueueFailedEvent = new(1051, "ScrapeEnqueueFailed");

    private readonly ILogger<TweetSubmissionService> _logger;

    public TweetSubmissionService(
        IAppDbContext db,
        IQueueService queue,
        IAsyncContext<IdentityContext> identityContext,
        ILogger<TweetSubmissionService> logger)
    {
        _db = db;
        _queue = queue;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task<Result<Tweet>> SubmitAsync(SubmitTweetCommand command, CancellationToken ct)
    {
        var match = TweetUrlRegex().Match(command.TweetUrl);
        if (!match.Success)
        {
            return Result.Failure<Tweet>(DomainError.Validation("Invalid tweet URL. Expected format: https://x.com/{username}/status/{id}"));
        }

        var authorXUsername = match.Groups[$"username"].Value;
        var xTweetId = match.Groups[$"id"].Value;

        var existing = await _db.Tweets.AsNoTracking().FirstOrDefaultAsync(t => t.XTweetId == xTweetId, ct);
        if (existing != null)
        {
            return Result.Failure<Tweet>(DomainError.Conflict($"Already submitted"));
        }

        var correlationId = Guid.NewGuid().ToString();
        var tweetId = Guid.NewGuid();
        var ipAddress = _identityContext.Value!.IpAddress;

        var auditPayload = JsonSerializer.Serialize(new
        {
            xTweetUrl = command.TweetUrl,
            xTweetId,
            authorXUsername,
            folderIds = command.FolderIds,
            submittedByUserId = command.SubmittedByUserId,
            submittedByIp = ipAddress,
        });

        _db.AuditLogs.Add(new AuditLogRecord
        {
            CorrelationId = correlationId,
            Action = $"Tweet.SubmitRequest",
            EntityType = $"Tweet",
            EntityId = xTweetId,
            PerformedByUserId = command.SubmittedByUserId,
            IpAddress = ipAddress,
            Payload = auditPayload,
        });

        var tweetRecord = new TweetRecord
        {
            Id = tweetId,
            XTweetId = xTweetId,
            XTweetUrl = command.TweetUrl,
            AuthorXUsername = authorXUsername,
            FetchStatus = $"Pending",
            SubmittedByUserId = command.SubmittedByUserId,
            SubmittedByIp = ipAddress,
        };

        _db.Tweets.Add(tweetRecord);

        // Explicit save required: the scrape queue message below depends on the tweet being persisted.
        await _db.SaveChangesAsync(ct);

        try
        {
            await _queue.EnqueueAsync(
                new ScrapeJobMessage(
                    XTweetUrl: command.TweetUrl,
                    XTweetId: xTweetId,
                    AuthorXUsername: authorXUsername,
                    FolderIds: command.FolderIds,
                    SubmittedByUserId: command.SubmittedByUserId,
                    SubmittedByIp: ipAddress,
                    CorrelationId: correlationId),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(EnqueueFailedEvent, ex, "Failed to enqueue scrape job for tweet {XTweetId}. Audit log recorded with CorrelationId {CorrelationId}", xTweetId, correlationId);
        }

        _logger.LogInformation(TweetSubmittedEvent, "Tweet submitted: {TweetId}, XTweetId: {XTweetId}, CorrelationId: {CorrelationId}", tweetId, xTweetId, correlationId);

        return Result.Success(TweetMapper.ToDomain(tweetRecord));
    }

    [GeneratedRegex(@"https?://(?:x|twitter)\.com/(?<username>[^/]+)/status/(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TweetUrlRegex();
}
