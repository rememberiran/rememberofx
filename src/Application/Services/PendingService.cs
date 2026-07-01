using Application.Interfaces;
using Application.Models;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class PendingService : IPendingService
{
    private readonly IAppDbContext _db;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly ILogger<PendingService> _logger;

    private static readonly EventId PendingAdditionApprovedEvent = new(1029, "PendingAdditionApproved");
    private static readonly EventId PendingAdditionRejectedEvent = new(1030, "PendingAdditionRejected");

    public PendingService(
        IAppDbContext db,
        IAsyncContext<IdentityContext> identityContext,
        ILogger<PendingService> logger)
    {
        _db = db;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task<Result<List<PendingSubmission>>> GetPendingAdditionsAsync(CancellationToken ct)
    {
        var pendingFolderTweets = await _db.FolderTweets
            .AsNoTracking()
            .Include(ft => ft.Folder)
            .Include(ft => ft.Tweet)
                .ThenInclude(t => t.SubmittedByUser)
            .Where(ft => ft.Status == "pending" && ft.Folder.IsActive)
            .ToListAsync(ct);

        var authorIds = pendingFolderTweets
            .Where(ft => ft.Tweet.AuthorXUserId != null)
            .Select(ft => ft.Tweet.AuthorXUserId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var profiles = authorIds.Count > 0
            ? await _db.XUserProfiles
                .AsNoTracking()
                .Where(p => authorIds.Contains(p.XUserId))
                .ToDictionaryAsync(p => p.XUserId, ct)
            : new Dictionary<string, XUserProfileRecord>(StringComparer.Ordinal);

        var grouped = pendingFolderTweets
            .GroupBy(ft => ft.TweetId)
            .Select(g =>
            {
                var firstFt = g.First();
                var tweet = TweetMapper.ToDomain(firstFt.Tweet);
                var authorProfile = profiles.GetValueOrDefault(firstFt.Tweet.AuthorXUserId ?? string.Empty);
                var tweetWithAuthor = new TweetWithAuthor(
                    tweet,
                    authorProfile != null ? XUserProfileMapper.ToDomain(authorProfile) : null);
                var folders = g.Select(ft => new PendingFolder(ft.FolderId, ft.Folder.Name, ft.AddedAt)).ToList();
                return new PendingSubmission(tweetWithAuthor, folders);
            })
            .ToList();

        return Result.Success(grouped);
    }

    public async Task<Result> ApprovePendingAdditionAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var folderTweet = await _db.FolderTweets
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);

        if (folderTweet is null)
        {
            return Result.Failure(DomainError.NotFound("Submission not found"));
        }

        if (!string.Equals(folderTweet.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure(DomainError.Validation("Submission is not pending"));
        }

        folderTweet.Status = "approved";
        folderTweet.ReviewedAt = DateTime.UtcNow;

        _logger.LogInformation(
            PendingAdditionApprovedEvent,
            "Pending addition approved: tweet {TweetId} in folder {FolderId} by user {UserId}",
            tweetId,
            folderId,
            userId);

        return Result.Success();
    }

    public async Task<Result> RejectPendingAdditionAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var folderTweet = await _db.FolderTweets
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);

        if (folderTweet is null)
        {
            return Result.Failure(DomainError.NotFound("Submission not found"));
        }

        if (!string.Equals(folderTweet.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure(DomainError.Validation("Submission is not pending"));
        }

        folderTweet.Status = "rejected";
        folderTweet.ReviewedAt = DateTime.UtcNow;

        _logger.LogInformation(
            PendingAdditionRejectedEvent,
            "Pending addition rejected: tweet {TweetId} in folder {FolderId} by user {UserId}",
            tweetId,
            folderId,
            userId);

        return Result.Success();
    }
}
