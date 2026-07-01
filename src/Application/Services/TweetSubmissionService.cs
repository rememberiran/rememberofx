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
    private const int AuthenticatedUserDailyLimit = 500;
    private const int AnonymousIpDailyLimit = 100;
    private const int IpDailyLimit = 1000;

    private static readonly EventId TweetSubmittedEvent = new(1050, "TweetSubmitted");
    private static readonly EventId EnqueueFailedEvent = new(1051, "ScrapeEnqueueFailed");
    private static readonly EventId UserRateLimitedEvent = new(1052, "UserRateLimited");
    private static readonly EventId IpRateLimitedEvent = new(1053, "IpRateLimited");

    private readonly IAppDbContext _db;
    private readonly IQueueService _queue;
    private readonly IAsyncContext<IdentityContext> _identityContext;
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

    public async Task<Result<SubmissionResultWithQuota>> SubmitAsync(SubmitTweetCommand command, CancellationToken ct)
    {
        var match = TweetUrlRegex().Match(command.TweetUrl);
        if (!match.Success)
        {
            return Result.Failure<SubmissionResultWithQuota>(DomainError.Validation("Invalid tweet URL. Expected format: https://x.com/{username}/status/{id}"));
        }

        var authorXUsername = match.Groups[$"username"].Value;
        var xTweetId = match.Groups[$"id"].Value;

        var existing = await _db.Tweets.AsNoTracking().FirstOrDefaultAsync(t => t.XTweetId == xTweetId, ct);
        if (existing != null)
        {
            return Result.Failure<SubmissionResultWithQuota>(DomainError.Conflict($"Already submitted"));
        }

        var identity = _identityContext.Value!;
        var now = DateTime.UtcNow;

        var ipDailyCutoff = now.AddDays(-1);
        var ipCount = await _db.Tweets.AsNoTracking()
            .CountAsync(t => t.SubmittedByIp == identity.IpAddress && t.CreatedAt >= ipDailyCutoff, ct);

        if (identity.InternalUserId.HasValue)
        {
            var userDailyCutoff = now.AddDays(-1);
            var userDailyCount = await _db.Tweets.AsNoTracking()
                .CountAsync(t => t.SubmittedByUserId == identity.InternalUserId && t.CreatedAt >= userDailyCutoff, ct);
            if (userDailyCount >= AuthenticatedUserDailyLimit)
            {
                _logger.LogWarning(UserRateLimitedEvent, "Daily limit reached for user {UserId} ({Count}/{Limit})", identity.InternalUserId, userDailyCount, AuthenticatedUserDailyLimit);
                return Result.Failure<SubmissionResultWithQuota>(DomainError.TooManyRequests($"Daily submission limit of {AuthenticatedUserDailyLimit} reached"));
            }
        }
        else
        {
            if (ipCount >= AnonymousIpDailyLimit)
            {
                _logger.LogWarning(IpRateLimitedEvent, "Anonymous IP daily limit reached for {IpAddress} ({Count}/{Limit})", identity.IpAddress, ipCount, AnonymousIpDailyLimit);
                return Result.Failure<SubmissionResultWithQuota>(DomainError.TooManyRequests($"Daily submission limit of {AnonymousIpDailyLimit} per IP reached for anonymous users"));
            }
        }

        if (ipCount >= IpDailyLimit)
        {
            _logger.LogWarning(IpRateLimitedEvent, "IP daily limit reached for {IpAddress} ({Count}/{Limit})", identity.IpAddress, ipCount, IpDailyLimit);
            return Result.Failure<SubmissionResultWithQuota>(DomainError.TooManyRequests($"Daily submission limit of {IpDailyLimit} per IP reached"));
        }

        var correlationId = Guid.NewGuid().ToString();
        var tweetId = Guid.NewGuid();
        var ipAddress = identity.IpAddress;

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
            IsAnonymous = command.IsAnonymous,
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

        var quota = await ComputeQuotaAsync(identity, now, ct);

        return Result.Success(new SubmissionResultWithQuota(TweetMapper.ToDomain(tweetRecord), quota));
    }

    public async Task<Result<SubmissionQuota>> GetQuotaAsync(CancellationToken ct)
    {
        var identity = _identityContext.Value;
        if (identity?.InternalUserId is null)
        {
            return Result.Failure<SubmissionQuota>(DomainError.Unauthorized("User not authenticated"));
        }

        var quota = await ComputeQuotaAsync(identity, DateTime.UtcNow, ct);
        return Result.Success(quota);
    }

    private async Task<SubmissionQuota> ComputeQuotaAsync(IdentityContext identity, DateTime now, CancellationToken ct)
    {
        var dailyResetAt = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var dailyCutoff = now.AddDays(-1);

        int dailyLimit;
        int dailyCount;

        if (identity.InternalUserId.HasValue)
        {
            dailyLimit = AuthenticatedUserDailyLimit;
            dailyCount = await _db.Tweets.AsNoTracking()
                .CountAsync(t => t.SubmittedByUserId == identity.InternalUserId && t.CreatedAt >= dailyCutoff, ct);
        }
        else
        {
            dailyLimit = AnonymousIpDailyLimit;
            dailyCount = await _db.Tweets.AsNoTracking()
                .CountAsync(t => t.SubmittedByIp == identity.IpAddress && t.CreatedAt >= dailyCutoff, ct);
        }

        var dailyRemaining = Math.Max(0, dailyLimit - dailyCount);

        return new SubmissionQuota(
            dailyRemaining,
            dailyLimit,
            dailyResetAt,
            dailyRemaining,
            dailyLimit,
            dailyResetAt);
    }

    [GeneratedRegex(@"https?://(?:x|twitter)\.com/(?<username>[^/]+)/status/(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TweetUrlRegex();
}
