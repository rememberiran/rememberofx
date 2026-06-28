using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface IFolderService
{
    Task<Result<List<FolderSummary>>> ListRootFoldersAsync(CancellationToken ct);
    Task<Result<List<FolderSummary>>> ListByCreatorAsync(Guid userId, CancellationToken ct);
    Task<Result<Folder>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<List<FolderSummary>>> GetChildrenAsync(Guid id, CancellationToken ct);
    Task<Result<PagedResult<TweetWithAuthor>>> GetTweetsAsync(Guid folderId, string sort, int page, int pageSize, CancellationToken ct);
    Task<Result<Folder>> CreateAsync(string name, string? description, string? icon, Guid? parentFolderId, CancellationToken ct);
    Task<Result<Folder>> UpdateAsync(Guid id, string? name, string? description, string? icon, Guid? parentFolderId, CancellationToken ct);
    Task<Result> AddTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct);
    Task<Result> RemoveTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct);
}
