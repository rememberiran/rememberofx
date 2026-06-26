using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storage;

namespace Application.Services;

public class FolderService : IFolderService
{
    private readonly IAppDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly FolderSettings _settings;
    private readonly ILogger<FolderService> _logger;

    public FolderService(IAppDbContext db, IBlobStorageService blobStorage, IOptions<FolderSettings> settings, ILogger<FolderService> logger)
    {
        _db = db;
        _blobStorage = blobStorage;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<List<FolderSummaryDto>>> ListRootFoldersAsync(CancellationToken ct)
    {
        var folders = await _db.Folders
            .Where(f => f.ParentFolderId == null && f.IsActive)
            .Select(f => new FolderSummaryDto(
                f.Id,
                f.Name,
                f.Description,
                f.Children.Count(c => c.IsActive)))
            .ToListAsync(ct);

        return Result.Success(folders);
    }

    public async Task<Result<FolderDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var folder = await _db.Folders
            .Include(f => f.Children)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive, ct);

        if (folder is null)
            return Result.Failure<FolderDto>(DomainError.NotFound("Folder not found"));

        var children = folder.Children
            .Where(c => c.IsActive)
            .Select(c => new FolderSummaryDto(c.Id, c.Name, c.Description, 0))
            .ToList();

        var breadcrumb = await GetBreadcrumbAsync(folder, ct);

        var dto = new FolderDto(
            folder.Id,
            folder.Name,
            folder.Description,
            folder.ParentFolderId,
            children.Count,
            folder.CreatedAt,
            folder.IsActive,
            children,
            breadcrumb);

        return Result.Success(dto);
    }

    public async Task<Result<List<FolderSummaryDto>>> GetChildrenAsync(Guid id, CancellationToken ct)
    {
        var exists = await _db.Folders.AnyAsync(f => f.Id == id && f.IsActive, ct);
        if (!exists)
            return Result.Failure<List<FolderSummaryDto>>(DomainError.NotFound("Folder not found"));

        var children = await _db.Folders
            .Where(f => f.ParentFolderId == id && f.IsActive)
            .Select(f => new FolderSummaryDto(
                f.Id,
                f.Name,
                f.Description,
                f.Children.Count(c => c.IsActive)))
            .ToListAsync(ct);

        return Result.Success(children);
    }

    public async Task<Result<SearchTweetsResult>> GetTweetsAsync(Guid folderId, string sort, int page, int pageSize, CancellationToken ct)
    {
        var exists = await _db.Folders.AnyAsync(f => f.Id == folderId && f.IsActive, ct);
        if (!exists)
            return Result.Failure<SearchTweetsResult>(DomainError.NotFound("Folder not found"));

        var query = _db.FolderTweets
            .Where(ft => ft.FolderId == folderId)
            .Join(_db.Tweets, ft => ft.TweetId, t => t.Id, (ft, t) => t)
            .Where(t => t.FetchStatus == "Ok");

        var totalCount = await query.CountAsync(ct);

        var sorted = sort == "date"
            ? query.OrderByDescending(t => t.CreatedAt)
            : query.OrderByDescending(t => t.VoteCount).ThenByDescending(t => t.CreatedAt);

        var tweets = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var authorIds = tweets.Where(t => t.AuthorXUserId != null).Select(t => t.AuthorXUserId!).Distinct().ToList();
        var profiles = authorIds.Count > 0
            ? await _db.XUserProfiles.Where(p => authorIds.Contains(p.XUserId)).ToDictionaryAsync(p => p.XUserId, ct)
            : new Dictionary<string, XUserProfileRecord>();

        var items = tweets.Select(t => new TweetDto(
            t.Id, t.XTweetId, t.XTweetUrl, t.AuthorXUserId, t.AuthorXUsername,
            t.TweetText, t.TweetDate, _blobStorage.GetScreenshotSasUrl(t.ScreenshotBlobName),
            t.Tags, t.VoteCount, t.FetchStatus, t.CreatedAt,
            profiles.GetValueOrDefault(t.AuthorXUserId ?? "") is { } p
                ? new XUserProfileDto(p.Id, p.XUserId, p.ScrapedUsername, p.CustomName, p.Description)
                : null)).ToList();

        return Result.Success(new SearchTweetsResult(items, totalCount));
    }

    public async Task<Result<FolderDto>> CreateAsync(string name, string? description, Guid? parentFolderId, Guid createdByUserId, CancellationToken ct)
    {
        if (parentFolderId.HasValue)
        {
            var parent = await _db.Folders.FirstOrDefaultAsync(f => f.Id == parentFolderId && f.IsActive, ct);
            if (parent is null)
                return Result.Failure<FolderDto>(DomainError.NotFound("Parent folder not found"));

            var depth = await GetDepthAsync(parentFolderId, ct);
            if (depth + 1 > _settings.MaxDepth)
                return Result.Failure<FolderDto>(DomainError.Validation($"Maximum folder depth of {_settings.MaxDepth} exceeded"));
        }

        var userFolderCount = await _db.Folders.CountAsync(f => f.CreatedByUserId == createdByUserId && f.IsActive, ct);
        if (userFolderCount >= _settings.MaxPerContributor)
            return Result.Failure<FolderDto>(DomainError.Validation($"Maximum of {_settings.MaxPerContributor} folders per contributor exceeded"));

        var folder = new FolderRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ParentFolderId = parentFolderId,
            CreatedByUserId = createdByUserId
        };

        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Folder created: {FolderId} by user {UserId}", folder.Id, createdByUserId);

        return Result.Success(new FolderDto(
            folder.Id, folder.Name, folder.Description, folder.ParentFolderId,
            0, folder.CreatedAt, folder.IsActive));
    }

    public async Task<Result<FolderDto>> UpdateAsync(Guid id, string? name, string? description, Guid? parentFolderId, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == id && f.IsActive, ct);
        if (folder is null)
            return Result.Failure<FolderDto>(DomainError.NotFound("Folder not found"));

        if (parentFolderId.HasValue && parentFolderId != folder.ParentFolderId)
        {
            if (parentFolderId == id)
                return Result.Failure<FolderDto>(DomainError.Validation("A folder cannot be its own parent"));

            var parent = await _db.Folders.FirstOrDefaultAsync(f => f.Id == parentFolderId && f.IsActive, ct);
            if (parent is null)
                return Result.Failure<FolderDto>(DomainError.NotFound("Parent folder not found"));

            var depth = await GetDepthAsync(parentFolderId, ct);
            if (depth + 1 > _settings.MaxDepth)
                return Result.Failure<FolderDto>(DomainError.Validation($"Maximum folder depth of {_settings.MaxDepth} exceeded"));

            folder.ParentFolderId = parentFolderId;
        }

        if (name != null) folder.Name = name;
        if (description != null) folder.Description = description;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Folder updated: {FolderId}", id);

        var childCount = await _db.Folders.CountAsync(f => f.ParentFolderId == id && f.IsActive, ct);
        return Result.Success(new FolderDto(
            folder.Id, folder.Name, folder.Description, folder.ParentFolderId,
            childCount, folder.CreatedAt, folder.IsActive));
    }

    public async Task<Result> AddTweetAsync(Guid folderId, Guid tweetId, Guid addedByUserId, CancellationToken ct)
    {
        var folderExists = await _db.Folders.AnyAsync(f => f.Id == folderId && f.IsActive, ct);
        if (!folderExists)
            return Result.Failure(DomainError.NotFound("Folder not found"));

        var tweetExists = await _db.Tweets.AnyAsync(t => t.Id == tweetId, ct);
        if (!tweetExists)
            return Result.Failure(DomainError.NotFound("Tweet not found"));

        var alreadyAdded = await _db.FolderTweets.AnyAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);
        if (alreadyAdded)
            return Result.Failure(DomainError.Conflict("Tweet already in folder"));

        var tweetCount = await _db.FolderTweets.CountAsync(ft => ft.FolderId == folderId, ct);
        if (tweetCount >= _settings.MaxTweetsPerFolder)
            return Result.Failure(DomainError.Validation($"Maximum of {_settings.MaxTweetsPerFolder} tweets per folder exceeded"));

        _db.FolderTweets.Add(new FolderTweetRecord
        {
            FolderId = folderId,
            TweetId = tweetId,
            AddedByUserId = addedByUserId
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Tweet {TweetId} added to folder {FolderId}", tweetId, folderId);
        return Result.Success();
    }

    public async Task<Result> RemoveTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct)
    {
        var folderTweet = await _db.FolderTweets
            .FirstOrDefaultAsync(ft => ft.FolderId == folderId && ft.TweetId == tweetId, ct);

        if (folderTweet is null)
            return Result.Failure(DomainError.NotFound("Tweet not found in folder"));

        _db.FolderTweets.Remove(folderTweet);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Tweet {TweetId} removed from folder {FolderId}", tweetId, folderId);
        return Result.Success();
    }

    private async Task<List<FolderBreadcrumbDto>> GetBreadcrumbAsync(FolderRecord folder, CancellationToken ct)
    {
        var breadcrumbs = new List<FolderBreadcrumbDto>();
        var currentParentId = folder.ParentFolderId;

        while (currentParentId != null)
        {
            var parent = await _db.Folders.FirstOrDefaultAsync(f => f.Id == currentParentId, ct);
            if (parent is null) break;
            breadcrumbs.Insert(0, new FolderBreadcrumbDto(parent.Id, parent.Name));
            currentParentId = parent.ParentFolderId;
        }

        return breadcrumbs;
    }

    private async Task<int> GetDepthAsync(Guid? parentFolderId, CancellationToken ct)
    {
        var depth = 0;
        var currentId = parentFolderId;
        while (currentId != null)
        {
            depth++;
            var parent = await _db.Folders.FirstOrDefaultAsync(f => f.Id == currentId, ct);
            currentId = parent?.ParentFolderId;
        }
        return depth;
    }
}
