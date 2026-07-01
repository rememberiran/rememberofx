using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class RemovalRequestService : IRemovalRequestService
{
    private const int ContributorApprovalsRequired = 2;

    private readonly IAppDbContext _db;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly ILogger<RemovalRequestService> _logger;

    private static readonly EventId RemovalRequestSubmittedEvent = new(1060, "RemovalRequestSubmitted");
    private static readonly EventId RemovalRequestApprovedEvent = new(1061, "RemovalRequestApproved");
    private static readonly EventId RemovalRequestRejectedEvent = new(1062, "RemovalRequestRejected");
    private static readonly EventId TweetRemovedViaRequestEvent = new(1063, "TweetRemovedViaRequest");

    public RemovalRequestService(
        IAppDbContext db,
        IAsyncContext<IdentityContext> identityContext,
        ILogger<RemovalRequestService> logger)
    {
        _db = db;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task<Result<RemovalRequest>> SubmitAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var identity = _identityContext.Value!;

        var folderTweet = await _db.FolderTweets
            .Include(ft => ft.Folder)
            .Include(ft => ft.Tweet)
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId && ft.Status == "approved", ct);

        if (folderTweet is null)
        {
            return Result.Failure<RemovalRequest>(DomainError.NotFound("Tweet not found in folder"));
        }

        var alreadyPending = await _db.RemovalRequests.AnyAsync(
            r => r.FolderId == folderId && r.TweetId == tweetId && r.Status == "pending", ct);

        if (alreadyPending)
        {
            return Result.Failure<RemovalRequest>(DomainError.Conflict("A removal request for this tweet is already pending"));
        }

        var record = new FolderTweetRemovalRequestRecord
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            TweetId = tweetId,
            RequestedByUserId = identity.InternalUserId,
            RequestedByIp = identity.IpAddress,
            Status = "pending",
        };

        _db.RemovalRequests.Add(record);

        _logger.LogInformation(
            RemovalRequestSubmittedEvent,
            "Removal request submitted: {RequestId} for tweet {TweetId} in folder {FolderId}",
            record.Id,
            tweetId,
            folderId);

        return Result.Success(new RemovalRequest
        {
            Id = record.Id,
            FolderId = folderId,
            TweetId = tweetId,
            RequestedByUserId = record.RequestedByUserId,
            RequestedByIp = record.RequestedByIp,
            RequestedAt = record.RequestedAt,
            Status = record.Status,
            FolderName = folderTweet.Folder.Name,
            TweetXId = folderTweet.Tweet.XTweetId,
            Approvals = [],
        });
    }

    public async Task<Result<RemovalRequest>> ApproveAsync(Guid requestId, CancellationToken ct)
    {
        var identity = _identityContext.Value!;
        var userId = identity.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<RemovalRequest>(DomainError.Unauthorized("User not authenticated"));
        }

        var request = await _db.RemovalRequests
            .Include(r => r.Folder)
            .Include(r => r.Tweet)
            .Include(r => r.Approvals)
                .ThenInclude(a => a.ApprovedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null)
        {
            return Result.Failure<RemovalRequest>(DomainError.NotFound("Removal request not found"));
        }

        if (!string.Equals(request.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure<RemovalRequest>(DomainError.Validation("Removal request is not pending"));
        }

        var alreadyApproved = request.Approvals.Any(a => a.ApprovedByUserId == userId.Value && !a.IsVoid);
        if (alreadyApproved)
        {
            return Result.Failure<RemovalRequest>(DomainError.Conflict("You have already approved this request"));
        }

        var isAdmin = identity.Roles.Contains("Admin", StringComparer.Ordinal);

        if (isAdmin)
        {
            await ExecuteRemovalAsync(request, ct);
            request.Status = "approved";
            request.ResolvedAt = DateTime.UtcNow;
        }
        else
        {
            _db.RemovalApprovals.Add(new FolderTweetRemovalApprovalRecord
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                ApprovedByUserId = userId.Value,
            });

            var validApprovalCount = request.Approvals.Count(a => !a.IsVoid) + 1;
            if (validApprovalCount >= ContributorApprovalsRequired)
            {
                await ExecuteRemovalAsync(request, ct);
                request.Status = "approved";
                request.ResolvedAt = DateTime.UtcNow;
            }
        }

        _logger.LogInformation(
            RemovalRequestApprovedEvent,
            "Removal request {RequestId} approved by user {UserId}",
            requestId,
            userId);

        return Result.Success(RemovalRequestMapper.ToDomain(request));
    }

    public async Task<Result<RemovalRequest>> RejectAsync(Guid requestId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<RemovalRequest>(DomainError.Unauthorized("User not authenticated"));
        }

        var request = await _db.RemovalRequests
            .Include(r => r.Folder)
            .Include(r => r.Tweet)
            .Include(r => r.Approvals)
                .ThenInclude(a => a.ApprovedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null)
        {
            return Result.Failure<RemovalRequest>(DomainError.NotFound("Removal request not found"));
        }

        if (!string.Equals(request.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure<RemovalRequest>(DomainError.Validation("Removal request is not pending"));
        }

        request.Status = "rejected";
        request.ResolvedAt = DateTime.UtcNow;

        _logger.LogInformation(
            RemovalRequestRejectedEvent,
            "Removal request {RequestId} rejected by user {UserId}",
            requestId,
            userId);

        return Result.Success(RemovalRequestMapper.ToDomain(request));
    }

    public async Task<Result<List<RemovalRequest>>> GetPendingAsync(CancellationToken ct)
    {
        var records = await _db.RemovalRequests
            .AsNoTracking()
            .Include(r => r.Folder)
            .Include(r => r.Tweet)
            .Include(r => r.Approvals)
                .ThenInclude(a => a.ApprovedByUser)
            .Where(r => r.Status == "pending")
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(ct);

        return Result.Success(records.Select(RemovalRequestMapper.ToDomain).ToList());
    }

    private async Task ExecuteRemovalAsync(FolderTweetRemovalRequestRecord request, CancellationToken ct)
    {
        var folderTweet = await _db.FolderTweets
            .FirstOrDefaultAsync(ft => ft.FolderId == request.FolderId && ft.TweetId == request.TweetId, ct);

        if (folderTweet is not null)
        {
            _db.FolderTweets.Remove(folderTweet);
            _logger.LogInformation(
                TweetRemovedViaRequestEvent,
                "Tweet {TweetId} removed from folder {FolderId} via removal request {RequestId}",
                request.TweetId,
                request.FolderId,
                request.Id);
        }
    }
}
