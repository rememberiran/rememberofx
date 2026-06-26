using Application.Models;

namespace Application.Interfaces;

public interface IFolderService
{
    Task<Result<List<FolderSummaryDto>>> ListRootFoldersAsync(CancellationToken ct);
    Task<Result<FolderDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<List<FolderSummaryDto>>> GetChildrenAsync(Guid id, CancellationToken ct);
    Task<Result<SearchTweetsResult>> GetTweetsAsync(Guid folderId, string sort, int page, int pageSize, CancellationToken ct);
    Task<Result<FolderDto>> CreateAsync(string name, string? description, Guid? parentFolderId, Guid createdByUserId, CancellationToken ct);
    Task<Result<FolderDto>> UpdateAsync(Guid id, string? name, string? description, Guid? parentFolderId, CancellationToken ct);
    Task<Result> AddTweetAsync(Guid folderId, Guid tweetId, Guid addedByUserId, CancellationToken ct);
    Task<Result> RemoveTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct);
}
