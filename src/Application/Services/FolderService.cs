using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storage;

namespace Application.Services;

public class FolderService : IFolderService
{
    private readonly IAppDbContext _db;
    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly FolderSettings _settings;
    private static readonly EventId FolderCreatedEvent = new(1020, "FolderCreated");
    private static readonly EventId FolderUpdatedEvent = new(1021, "FolderUpdated");
    private static readonly EventId TweetAddedToFolderEvent = new(1022, "TweetAddedToFolder");
    private static readonly EventId TweetRemovedFromFolderEvent = new(1023, "TweetRemovedFromFolder");
    private static readonly EventId FoldersListedByCreatorEvent = new(1024, "FoldersListedByCreator");
    private static readonly EventId FolderDeletedEvent = new(1025, "FolderDeleted");
    private static readonly EventId FoldersSearchedEvent = new(1026, "FoldersSearched");
    private static readonly EventId TrustedContributorAddedEvent = new(1027, "TrustedContributorAdded");
    private static readonly EventId TrustedContributorRemovedEvent = new(1028, "TrustedContributorRemoved");
    private static readonly EventId SubmissionApprovedEvent = new(1029, "SubmissionApproved");
    private static readonly EventId SubmissionRejectedEvent = new(1030, "SubmissionRejected");

    private readonly ILogger<FolderService> _logger;

    public FolderService(
        IAppDbContext db,
        IAsyncContext<IdentityContext> identityContext,
        IOptions<FolderSettings> settings,
        ILogger<FolderService> logger)
    {
        _db = db;
        _identityContext = identityContext;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<List<FolderSummary>>> ListRootFoldersAsync(CancellationToken ct)
    {
        var folders = await _db.Folders.AsNoTracking()
            .Include(f => f.CreatedByUser)
            .Where(f => f.ParentFolderId == null && f.IsActive)
            .Select(f => new { Record = f, ChildCount = f.Children.Count(c => c.IsActive) })
            .ToListAsync(ct);

        var folderIds = folders.Select(f => f.Record.Id).ToList();
        var cumulativeCounts = await GetCumulativeTweetCountsAsync(folderIds, ct);

        return Result.Success(folders
            .Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, cumulativeCounts.GetValueOrDefault(f.Record.Id)))
            .ToList());
    }

    public async Task<Result<List<FolderSummary>>> ListByCreatorAsync(Guid userId, CancellationToken ct)
    {
        var folders = await _db.Folders.AsNoTracking()
            .Include(f => f.CreatedByUser)
            .Where(f => f.CreatedByUserId == userId && f.IsActive)
            .Select(f => new
            {
                Record = f,
                ChildCount = f.Children.Count(c => c.IsActive),
            })
            .ToListAsync(ct);

        _logger.LogInformation(FoldersListedByCreatorEvent, "Listed {FolderCount} folders for user {UserId}", folders.Count, userId);

        var folderIds = folders.Select(f => f.Record.Id).ToList();
        var cumulativeCounts = await GetCumulativeTweetCountsAsync(folderIds, ct);

        return Result.Success(folders
            .Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, cumulativeCounts.GetValueOrDefault(f.Record.Id)))
            .ToList());
    }

    public async Task<Result<Folder>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var folder = await _db.Folders.AsNoTracking()
            .Include(f => f.Children)
            .Include(f => f.CreatedByUser)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive, ct);

        if (folder is null)
        {
            return Result.Failure<Folder>(DomainError.NotFound($"Folder not found"));
        }

        var domainFolder = FolderMapper.ToDomain(folder);
        domainFolder.Children = folder.Children
            .Where(c => c.IsActive)
            .Select(c => FolderMapper.ToDomain(c))
            .ToList();

        var breadcrumb = await GetBreadcrumbAsync(folder, ct);
        domainFolder.ParentFolder = BuildBreadcrumbChain(breadcrumb);

        return Result.Success(domainFolder);
    }

    public async Task<Result<List<FolderSummary>>> GetChildrenAsync(Guid id, CancellationToken ct)
    {
        var exists = await _db.Folders.AnyAsync(f => f.Id == id && f.IsActive, ct);
        if (!exists)
        {
            return Result.Failure<List<FolderSummary>>(DomainError.NotFound($"Folder not found"));
        }

        var children = await _db.Folders.AsNoTracking()
            .Include(f => f.CreatedByUser)
            .Where(f => f.ParentFolderId == id && f.IsActive)
            .Select(f => new { Record = f, ChildCount = f.Children.Count(c => c.IsActive) })
            .ToListAsync(ct);

        var childIds = children.Select(f => f.Record.Id).ToList();
        var cumulativeCounts = await GetCumulativeTweetCountsAsync(childIds, ct);

        return Result.Success(children
            .Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, cumulativeCounts.GetValueOrDefault(f.Record.Id)))
            .ToList());
    }

    public async Task<Result<PagedResult<TweetWithAuthor>>> GetTweetsAsync(Guid folderId, string sort, int page, int pageSize, CancellationToken ct)
    {
        var exists = await _db.Folders.AnyAsync(f => f.Id == folderId && f.IsActive, ct);
        if (!exists)
        {
            return Result.Failure<PagedResult<TweetWithAuthor>>(DomainError.NotFound($"Folder not found"));
        }

        var folderTweetIds = _db.FolderTweets
            .Where(ft => ft.FolderId == folderId && ft.Status == "approved")
            .Select(ft => ft.TweetId);

        var query = _db.Tweets
            .Where(t => folderTweetIds.Contains(t.Id) && t.FetchStatus == "Ok")
            .Include(t => t.SubmittedByUser)
            .Include(t => t.FolderTweets)
                .ThenInclude(ft => ft.Folder)
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var sorted = string.Equals(sort, $"date", StringComparison.Ordinal)
            ? query.OrderByDescending(t => t.CreatedAt)
            : query.OrderByDescending(t => t.VoteCount).ThenByDescending(t => t.CreatedAt);

        var tweets = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var authorIds = tweets.Where(t => t.AuthorXUserId != null).Select(t => t.AuthorXUserId!).Distinct(StringComparer.Ordinal).ToList();
        var profiles = authorIds.Count > 0
            ? await _db.XUserProfiles.AsNoTracking().Where(p => authorIds.Contains(p.XUserId)).ToDictionaryAsync(p => p.XUserId, ct)
            : new Dictionary<string, XUserProfileRecord>(StringComparer.Ordinal);

        var items = tweets.Select(t =>
        {
            var authorProfile = profiles.GetValueOrDefault(t.AuthorXUserId ?? string.Empty);
            return new TweetWithAuthor(
                TweetMapper.ToDomain(t),
                authorProfile != null ? XUserProfileMapper.ToDomain(authorProfile) : null);
        }).ToList();

        return Result.Success(new PagedResult<TweetWithAuthor>(items, totalCount));
    }

    public async Task<Result<Folder>> CreateAsync(string name, string? description, string? icon, string? visibility, Guid? parentFolderId, CancellationToken ct)
    {
        if (parentFolderId.HasValue)
        {
            var parent = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == parentFolderId && f.IsActive, ct);
            if (parent is null)
            {
                return Result.Failure<Folder>(DomainError.NotFound($"Parent folder not found"));
            }

            var depth = await GetParentDepthAsync(parentFolderId, ct);
            if (depth + 1 > _settings.MaxDepth)
            {
                return Result.Failure<Folder>(DomainError.Validation($"Maximum folder depth of {_settings.MaxDepth} exceeded"));
            }
        }

        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<Folder>(DomainError.Unauthorized("User not authenticated"));
        }

        var userFolderCount = await _db.Folders.CountAsync(f => f.CreatedByUserId == userId && f.IsActive, ct);
        if (userFolderCount >= _settings.MaxPerContributor)
        {
            return Result.Failure<Folder>(DomainError.Validation($"Maximum of {_settings.MaxPerContributor} folders per contributor exceeded"));
        }

        var folder = new FolderRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Icon = icon,
            Visibility = visibility ?? "private",
            ParentFolderId = parentFolderId,
            CreatedByUserId = userId.Value,
        };

        _db.Folders.Add(folder);

        _db.FolderClosures.Add(new FolderClosureRecord
        {
            AncestorId = folder.Id,
            DescendantId = folder.Id,
            Depth = 0,
        });

        if (parentFolderId.HasValue)
        {
            var parentAncestors = await _db.FolderClosures
                .AsNoTracking()
                .Where(fc => fc.DescendantId == parentFolderId.Value)
                .ToListAsync(ct);

            foreach (var pa in parentAncestors)
            {
                _db.FolderClosures.Add(new FolderClosureRecord
                {
                    AncestorId = pa.AncestorId,
                    DescendantId = folder.Id,
                    Depth = pa.Depth + 1,
                });
            }
        }

        _logger.LogInformation(FolderCreatedEvent, "Folder created: {FolderId} by user {UserId}", folder.Id, userId);

        return Result.Success(FolderMapper.ToDomain(folder));
    }

    public async Task<Result<Folder>> UpdateAsync(Guid id, string? name, string? description, string? icon, string? visibility, Guid? parentFolderId, CancellationToken ct)
    {
        var folder = await _db.Folders
            .Include(f => f.CreatedByUser)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive, ct);

        if (folder is null)
        {
            return Result.Failure<Folder>(DomainError.NotFound($"Folder not found"));
        }

        if (parentFolderId.HasValue && parentFolderId != folder.ParentFolderId)
        {
            var wouldCycle = await _db.FolderClosures
                .AnyAsync(fc => fc.AncestorId == id && fc.DescendantId == parentFolderId.Value, ct);
            if (wouldCycle)
            {
                return Result.Failure<Folder>(DomainError.Validation("Cannot move a folder under one of its own descendants"));
            }

            var parent = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == parentFolderId && f.IsActive, ct);
            if (parent is null)
            {
                return Result.Failure<Folder>(DomainError.NotFound($"Parent folder not found"));
            }

            var depth = await GetParentDepthAsync(parentFolderId, ct);
            if (depth + 1 > _settings.MaxDepth)
            {
                return Result.Failure<Folder>(DomainError.Validation($"Maximum folder depth of {_settings.MaxDepth} exceeded"));
            }

            var subtreeDescendantIds = await _db.FolderClosures
                .Where(fc => fc.AncestorId == id)
                .Select(fc => fc.DescendantId)
                .ToListAsync(ct);

            var subtreeRelativeDepths = await _db.FolderClosures
                .Where(fc => fc.AncestorId == id && subtreeDescendantIds.Contains(fc.DescendantId))
                .ToDictionaryAsync(fc => fc.DescendantId, fc => fc.Depth, ct);

            var oldPaths = await _db.FolderClosures
                .Where(fc => subtreeDescendantIds.Contains(fc.DescendantId)
                          && !subtreeDescendantIds.Contains(fc.AncestorId))
                .ToListAsync(ct);

            _db.FolderClosures.RemoveRange(oldPaths);

            var newParentAncestors = await _db.FolderClosures
                .AsNoTracking()
                .Where(fc => fc.DescendantId == parentFolderId.Value)
                .ToListAsync(ct);

            foreach (var ancestor in newParentAncestors)
            {
                foreach (var (descendantId, relativeDepth) in subtreeRelativeDepths)
                {
                    _db.FolderClosures.Add(new FolderClosureRecord
                    {
                        AncestorId = ancestor.AncestorId,
                        DescendantId = descendantId,
                        Depth = ancestor.Depth + 1 + relativeDepth,
                    });
                }
            }

            folder.ParentFolderId = parentFolderId;
        }

        if (name != null)
        {
            folder.Name = name;
        }

        if (description != null)
        {
            folder.Description = description;
        }

        if (icon != null)
        {
            folder.Icon = icon;
        }

        if (visibility != null)
        {
            folder.Visibility = visibility;
        }

        _logger.LogInformation(FolderUpdatedEvent, "Folder updated: {FolderId}", id);

        return Result.Success(FolderMapper.ToDomain(folder));
    }

    public async Task<Result> AddTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<Folder>(DomainError.Unauthorized("User not authenticated"));
        }

        var folder = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId && f.IsActive, ct);
        if (folder is null)
        {
            return Result.Failure(DomainError.NotFound($"Folder not found"));
        }

        var tweetExists = await _db.Tweets.AnyAsync(t => t.Id == tweetId, ct);
        if (!tweetExists)
        {
            return Result.Failure(DomainError.NotFound($"Tweet not found"));
        }

        var alreadyAdded = await _db.FolderTweets.AnyAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);
        if (alreadyAdded)
        {
            return Result.Failure(DomainError.Conflict($"Tweet already in folder"));
        }

        var tweetCount = await _db.FolderTweets.CountAsync(ft => ft.FolderId == folderId, ct);
        if (tweetCount >= _settings.MaxTweetsPerFolder)
        {
            return Result.Failure(DomainError.Validation($"Maximum of {_settings.MaxTweetsPerFolder} tweets per folder exceeded"));
        }

        var isOwner = folder.CreatedByUserId == userId.Value;

        if (string.Equals(folder.Visibility, "private", StringComparison.Ordinal) && !isOwner)
        {
            return Result.Failure(DomainError.Forbidden("Only the folder owner can add tweets to a private folder"));
        }

        var status = "approved";
        if (!isOwner)
        {
            var callerXUsername = _identityContext.Value?.XUsername;
            var isTrusted = callerXUsername != null && await _db.TrustedContributors.AnyAsync(
                tc => tc.OwnerUserId == folder.CreatedByUserId && tc.TrustedXUsername == callerXUsername,
                ct);

            if (!isTrusted)
            {
                status = "pending";
            }
        }

        _db.FolderTweets.Add(new FolderTweetRecord
        {
            FolderId = folderId,
            TweetId = tweetId,
            AddedByUserId = userId.Value,
            Status = status,
        });

        _logger.LogInformation(TweetAddedToFolderEvent, "Tweet {TweetId} added to folder {FolderId} with status {Status}", tweetId, folderId, status);
        return Result.Success();
    }

    public async Task<Result> RemoveTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var folderTweet = await _db.FolderTweets
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);

        if (folderTweet is null)
        {
            return Result.Failure(DomainError.NotFound($"Tweet not found in folder"));
        }

        _db.FolderTweets.Remove(folderTweet);

        _logger.LogInformation(TweetRemovedFromFolderEvent, "Tweet {TweetId} removed from folder {FolderId}", tweetId, folderId);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == id && f.IsActive, ct);
        if (folder is null)
        {
            return Result.Failure(DomainError.NotFound("Folder not found"));
        }

        if (folder.CreatedByUserId != userId.Value)
        {
            return Result.Failure(DomainError.Forbidden("Only the folder owner can delete it"));
        }

        var descendantIds = await _db.FolderClosures
            .Where(fc => fc.AncestorId == id && fc.Depth > 0)
            .Select(fc => fc.DescendantId)
            .ToListAsync(ct);

        folder.IsActive = false;

        if (descendantIds.Count > 0)
        {
            var descendants = await _db.Folders
                .Where(f => descendantIds.Contains(f.Id) && f.IsActive)
                .ToListAsync(ct);

            foreach (var d in descendants)
            {
                d.IsActive = false;
            }
        }

        _logger.LogInformation(FolderDeletedEvent, "Folder {FolderId} deleted by user {UserId}, {DescendantCount} descendants deactivated", id, userId, descendantIds.Count);

        return Result.Success();
    }

    public async Task<Result<List<FolderSummary>>> SearchFoldersAsync(string query, CancellationToken ct)
    {
        var pattern = $"%{query}%";
        var folders = await _db.Folders.AsNoTracking()
            .Include(f => f.CreatedByUser)
            .Where(f => f.IsActive && EF.Functions.Like(f.Name, pattern))
            .OrderByDescending(f => f.Children.Count)
            .Take(20)
            .Select(f => new { Record = f, ChildCount = f.Children.Count(c => c.IsActive) })
            .ToListAsync(ct);

        var folderIds = folders.Select(f => f.Record.Id).ToList();
        var cumulativeCounts = await GetCumulativeTweetCountsAsync(folderIds, ct);

        _logger.LogInformation(FoldersSearchedEvent, "Folder search for '{Query}' returned {Count} results", query, folders.Count);

        return Result.Success(folders
            .Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, cumulativeCounts.GetValueOrDefault(f.Record.Id)))
            .ToList());
    }

    public async Task<Result<List<FolderSummary>>> GetValidMoveTargetsAsync(Guid folderId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<List<FolderSummary>>(DomainError.Unauthorized("User not authenticated"));
        }

        var node = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId && f.IsActive, ct);
        if (node is null)
        {
            return Result.Failure<List<FolderSummary>>(DomainError.NotFound("Folder not found"));
        }

        var descendantIds = await _db.FolderClosures
            .Where(fc => fc.AncestorId == folderId)
            .Select(fc => fc.DescendantId)
            .ToListAsync(ct);

        var subtreeHeight = await _db.FolderClosures
            .Where(fc => fc.AncestorId == folderId)
            .MaxAsync(fc => (int?)fc.Depth, ct) ?? 0;

        var excludeIds = new HashSet<Guid>(descendantIds);

        var currentParentId = node.ParentFolderId;

        var candidates = await _db.Folders.AsNoTracking()
            .Include(f => f.CreatedByUser)
            .Where(f => f.IsActive && f.CreatedByUserId == userId.Value && !excludeIds.Contains(f.Id))
            .Select(f => new
            {
                Record = f,
                ChildCount = f.Children.Count(c => c.IsActive),
            })
            .ToListAsync(ct);

        var candidateIds = candidates.Select(c => c.Record.Id).ToList();
        var depths = await _db.FolderClosures
            .Where(fc => fc.DescendantId == fc.AncestorId && candidateIds.Contains(fc.DescendantId))
            .Join(
                _db.FolderClosures.Where(fc2 => candidateIds.Contains(fc2.DescendantId)),
                self => self.DescendantId,
                other => other.DescendantId,
                (self, other) => new { other.DescendantId, other.Depth })
            .GroupBy(x => x.DescendantId)
            .Select(g => new { FolderId = g.Key, MaxDepth = g.Max(x => x.Depth) })
            .ToDictionaryAsync(x => x.FolderId, x => x.MaxDepth, ct);

        var cumulativeCounts = await GetCumulativeTweetCountsAsync(candidateIds, ct);

        var validTargets = candidates
            .Where(c =>
            {
                if (c.Record.Id == currentParentId)
                {
                    return false;
                }

                var targetDepth = depths.GetValueOrDefault(c.Record.Id);
                return targetDepth + 1 + subtreeHeight <= _settings.MaxDepth;
            })
            .Select(c => new FolderSummary(
                FolderMapper.ToDomain(c.Record),
                c.ChildCount,
                cumulativeCounts.GetValueOrDefault(c.Record.Id)))
            .ToList();

        return Result.Success(validTargets);
    }

    public async Task<Result<int>> GetDepthAsync(Guid folderId, CancellationToken ct)
    {
        var exists = await _db.Folders.AnyAsync(f => f.Id == folderId && f.IsActive, ct);
        if (!exists)
        {
            return Result.Failure<int>(DomainError.NotFound("Folder not found"));
        }

        var depth = await _db.FolderClosures
            .Where(fc => fc.DescendantId == folderId)
            .MaxAsync(fc => (int?)fc.Depth, ct) ?? 0;

        return Result.Success(depth + 1);
    }

    public async Task<Result<List<TrustedContributor>>> GetTrustedContributorsAsync(CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<List<TrustedContributor>>(DomainError.Unauthorized("User not authenticated"));
        }

        var records = await _db.TrustedContributors.AsNoTracking()
            .Where(tc => tc.OwnerUserId == userId.Value)
            .OrderBy(tc => tc.TrustedXUsername)
            .ToListAsync(ct);

        return Result.Success(records.Select(TrustedContributorMapper.ToDomain).ToList());
    }

    public async Task<Result> AddTrustedContributorAsync(string trustedXUsername, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var exists = await _db.TrustedContributors.AnyAsync(
            tc => tc.OwnerUserId == userId.Value && tc.TrustedXUsername == trustedXUsername,
            ct);

        if (exists)
        {
            return Result.Failure(DomainError.Conflict("User is already trusted"));
        }

        _db.TrustedContributors.Add(new TrustedContributorRecord
        {
            OwnerUserId = userId.Value,
            TrustedXUsername = trustedXUsername,
        });

        _logger.LogInformation(TrustedContributorAddedEvent, "Trusted contributor {TrustedXUsername} added by user {UserId}", trustedXUsername, userId);
        return Result.Success();
    }

    public async Task<Result> RemoveTrustedContributorAsync(string trustedXUsername, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var record = await _db.TrustedContributors.FirstOrDefaultAsync(
            tc => tc.OwnerUserId == userId.Value && tc.TrustedXUsername == trustedXUsername,
            ct);

        if (record is null)
        {
            return Result.Failure(DomainError.NotFound("Trusted contributor not found"));
        }

        _db.TrustedContributors.Remove(record);

        _logger.LogInformation(TrustedContributorRemovedEvent, "Trusted contributor {TrustedXUsername} removed by user {UserId}", trustedXUsername, userId);
        return Result.Success();
    }

    public async Task<Result<List<PendingSubmission>>> GetPendingSubmissionsAsync(CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<List<PendingSubmission>>(DomainError.Unauthorized("User not authenticated"));
        }

        var pendingFolderTweets = await _db.FolderTweets.AsNoTracking()
            .Include(ft => ft.Folder)
            .Include(ft => ft.Tweet)
                .ThenInclude(t => t.SubmittedByUser)
            .Where(ft => ft.Status == "pending" && ft.Folder.CreatedByUserId == userId.Value && ft.Folder.IsActive)
            .ToListAsync(ct);

        var tweetIds = pendingFolderTweets.Select(ft => ft.TweetId).Distinct().ToList();

        var authorIds = pendingFolderTweets
            .Where(ft => ft.Tweet.AuthorXUserId != null)
            .Select(ft => ft.Tweet.AuthorXUserId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var profiles = authorIds.Count > 0
            ? await _db.XUserProfiles.AsNoTracking()
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

    public async Task<Result> ApproveSubmissionAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var folderTweet = await _db.FolderTweets
            .Include(ft => ft.Folder)
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);

        if (folderTweet is null)
        {
            return Result.Failure(DomainError.NotFound("Submission not found"));
        }

        if (folderTweet.Folder.CreatedByUserId != userId.Value)
        {
            return Result.Failure(DomainError.Forbidden("Only the folder owner can approve submissions"));
        }

        if (!string.Equals(folderTweet.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure(DomainError.Validation("Submission is not pending"));
        }

        folderTweet.Status = "approved";
        folderTweet.ReviewedAt = DateTime.UtcNow;

        _logger.LogInformation(SubmissionApprovedEvent, "Submission approved: tweet {TweetId} in folder {FolderId} by user {UserId}", tweetId, folderId, userId);
        return Result.Success();
    }

    public async Task<Result> RejectSubmissionAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure(DomainError.Unauthorized("User not authenticated"));
        }

        var folderTweet = await _db.FolderTweets
            .Include(ft => ft.Folder)
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);

        if (folderTweet is null)
        {
            return Result.Failure(DomainError.NotFound("Submission not found"));
        }

        if (folderTweet.Folder.CreatedByUserId != userId.Value)
        {
            return Result.Failure(DomainError.Forbidden("Only the folder owner can reject submissions"));
        }

        if (!string.Equals(folderTweet.Status, "pending", StringComparison.Ordinal))
        {
            return Result.Failure(DomainError.Validation("Submission is not pending"));
        }

        folderTweet.Status = "rejected";
        folderTweet.ReviewedAt = DateTime.UtcNow;

        _logger.LogInformation(SubmissionRejectedEvent, "Submission rejected: tweet {TweetId} in folder {FolderId} by user {UserId}", tweetId, folderId, userId);
        return Result.Success();
    }

    public async Task<Result<ContributionStats>> GetContributionStatsAsync(CancellationToken ct)
    {
        var userId = _identityContext.Value?.InternalUserId;
        if (userId is null)
        {
            return Result.Failure<ContributionStats>(DomainError.Unauthorized("User not authenticated"));
        }

        var myFolderIds = await _db.Folders.AsNoTracking()
            .Where(f => f.CreatedByUserId == userId.Value && f.IsActive)
            .Select(f => f.Id)
            .ToListAsync(ct);

        if (myFolderIds.Count == 0)
        {
            return Result.Success(new ContributionStats(0, 0));
        }

        var addedByOwner = await _db.FolderTweets.CountAsync(
            ft => myFolderIds.Contains(ft.FolderId) && ft.AddedByUserId == userId.Value && ft.Status == "approved",
            ct);

        var contributedByCommunity = await _db.FolderTweets.CountAsync(
            ft => myFolderIds.Contains(ft.FolderId) && ft.AddedByUserId != userId.Value && ft.Status == "approved",
            ct);

        return Result.Success(new ContributionStats(addedByOwner, contributedByCommunity));
    }

    public async Task<Result<ContributionStats>> GetFolderContributionStatsAsync(Guid folderId, CancellationToken ct)
    {
        var folder = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId && f.IsActive, ct);
        if (folder is null)
        {
            return Result.Failure<ContributionStats>(DomainError.NotFound("Folder not found"));
        }

        var addedByOwner = await _db.FolderTweets.CountAsync(
            ft => ft.FolderId == folderId && ft.AddedByUserId == folder.CreatedByUserId && ft.Status == "approved",
            ct);

        var contributedByCommunity = await _db.FolderTweets.CountAsync(
            ft => ft.FolderId == folderId && ft.AddedByUserId != folder.CreatedByUserId && ft.Status == "approved",
            ct);

        return Result.Success(new ContributionStats(addedByOwner, contributedByCommunity));
    }

    private async Task<List<Folder>> GetBreadcrumbAsync(FolderRecord folder, CancellationToken ct)
    {
        var breadcrumbs = new List<Folder>();
        var currentParentId = folder.ParentFolderId;

        while (currentParentId != null)
        {
            var parent = await _db.Folders.AsNoTracking()
                .Include(f => f.CreatedByUser)
                .FirstOrDefaultAsync(f => f.Id == currentParentId, ct);

            if (parent is null)
            {
                break;
            }

            breadcrumbs.Insert(0, FolderMapper.ToDomain(parent));
            currentParentId = parent.ParentFolderId;
        }

        return breadcrumbs;
    }

    private static Folder? BuildBreadcrumbChain(List<Folder> breadcrumbs)
    {
        if (breadcrumbs.Count == 0)
        {
            return null;
        }

        for (var i = breadcrumbs.Count - 1; i > 0; i--)
        {
            breadcrumbs[i].ParentFolder = breadcrumbs[i - 1];
        }

        return breadcrumbs[^1];
    }

    private async Task<Dictionary<Guid, int>> GetCumulativeTweetCountsAsync(List<Guid> folderIds, CancellationToken ct)
    {
        if (folderIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.FolderClosures
            .Where(fc => folderIds.Contains(fc.AncestorId))
            .Join(
                _db.Folders.Where(f => f.IsActive),
                fc => fc.DescendantId,
                f => f.Id,
                (fc, f) => new { fc.AncestorId, FolderId = f.Id })
            .Join(
                _db.FolderTweets,
                x => x.FolderId,
                ft => ft.FolderId,
                (x, ft) => new { x.AncestorId, ft.TweetId })
            .GroupBy(x => x.AncestorId)
            .Select(g => new { FolderId = g.Key, Count = g.Select(x => x.TweetId).Distinct().Count() })
            .ToDictionaryAsync(x => x.FolderId, x => x.Count, ct);
    }

    private async Task<int> GetParentDepthAsync(Guid? parentFolderId, CancellationToken ct)
    {
        if (!parentFolderId.HasValue)
        {
            return 0;
        }

        return await _db.FolderClosures
            .Where(fc => fc.DescendantId == parentFolderId.Value)
            .MaxAsync(fc => fc.Depth, ct) + 1;
    }
}
