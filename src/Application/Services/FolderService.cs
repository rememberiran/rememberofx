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
            .Select(f => new { Record = f, ChildCount = f.Children.Count(c => c.IsActive), TweetCount = f.FolderTweets.Count })
            .ToListAsync(ct);

        return Result.Success(folders.Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, f.TweetCount)).ToList());
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
                TweetCount = f.FolderTweets.Count,
            })
            .ToListAsync(ct);

        _logger.LogInformation(FoldersListedByCreatorEvent, "Listed {FolderCount} folders for user {UserId}", folders.Count, userId);

        return Result.Success(folders
            .Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, f.TweetCount))
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
            .Select(f => new { Record = f, ChildCount = f.Children.Count(c => c.IsActive), TweetCount = f.FolderTweets.Count })
            .ToListAsync(ct);

        return Result.Success(children.Select(f => new FolderSummary(FolderMapper.ToDomain(f.Record), f.ChildCount, f.TweetCount)).ToList());
    }

    public async Task<Result<PagedResult<TweetWithAuthor>>> GetTweetsAsync(Guid folderId, string sort, int page, int pageSize, CancellationToken ct)
    {
        var exists = await _db.Folders.AnyAsync(f => f.Id == folderId && f.IsActive, ct);
        if (!exists)
        {
            return Result.Failure<PagedResult<TweetWithAuthor>>(DomainError.NotFound($"Folder not found"));
        }

        var folderTweetIds = _db.FolderTweets
            .Where(ft => ft.FolderId == folderId)
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

    public async Task<Result<Folder>> CreateAsync(string name, string? description, string? icon, Guid? parentFolderId, CancellationToken ct)
    {
        if (parentFolderId.HasValue)
        {
            var parent = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == parentFolderId && f.IsActive, ct);
            if (parent is null)
            {
                return Result.Failure<Folder>(DomainError.NotFound($"Parent folder not found"));
            }

            var depth = await GetDepthAsync(parentFolderId, ct);
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
            ParentFolderId = parentFolderId,
            CreatedByUserId = userId.Value,
        };

        _db.Folders.Add(folder);

        _logger.LogInformation(FolderCreatedEvent, "Folder created: {FolderId} by user {UserId}", folder.Id, userId);

        return Result.Success(FolderMapper.ToDomain(folder));
    }

    public async Task<Result<Folder>> UpdateAsync(Guid id, string? name, string? description, string? icon, Guid? parentFolderId, CancellationToken ct)
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
            if (parentFolderId == id)
            {
                return Result.Failure<Folder>(DomainError.Validation($"A folder cannot be its own parent"));
            }

            var parent = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == parentFolderId && f.IsActive, ct);
            if (parent is null)
            {
                return Result.Failure<Folder>(DomainError.NotFound($"Parent folder not found"));
            }

            var depth = await GetDepthAsync(parentFolderId, ct);
            if (depth + 1 > _settings.MaxDepth)
            {
                return Result.Failure<Folder>(DomainError.Validation($"Maximum folder depth of {_settings.MaxDepth} exceeded"));
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

        var folderExists = await _db.Folders.AnyAsync(f => f.Id == folderId && f.IsActive, ct);
        if (!folderExists)
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

        _db.FolderTweets.Add(new FolderTweetRecord
        {
            FolderId = folderId,
            TweetId = tweetId,
            AddedByUserId = userId.Value,
        });

        _logger.LogInformation(TweetAddedToFolderEvent, "Tweet {TweetId} added to folder {FolderId}", tweetId, folderId);
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

    private async Task<int> GetDepthAsync(Guid? parentFolderId, CancellationToken ct)
    {
        var depth = 0;
        var currentId = parentFolderId;
        while (currentId != null)
        {
            depth++;
            var parent = await _db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == currentId, ct);
            currentId = parent?.ParentFolderId;
        }

        return depth;
    }
}
