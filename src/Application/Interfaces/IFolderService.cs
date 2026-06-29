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
    Task<Result<Folder>> CreateAsync(string name, string? description, string? icon, string? visibility, Guid? parentFolderId, CancellationToken ct);
    Task<Result<Folder>> UpdateAsync(Guid id, string? name, string? description, string? icon, string? visibility, Guid? parentFolderId, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct);
    Task<Result> AddTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct);
    Task<Result> RemoveTweetAsync(Guid folderId, Guid tweetId, CancellationToken ct);
    Task<Result<List<FolderSummary>>> SearchFoldersAsync(string query, CancellationToken ct);
    Task<Result<List<FolderSummary>>> GetValidMoveTargetsAsync(Guid folderId, CancellationToken ct);
    Task<Result<int>> GetDepthAsync(Guid folderId, CancellationToken ct);

    Task<Result<List<TrustedContributor>>> GetTrustedContributorsAsync(CancellationToken ct);
    Task<Result> AddTrustedContributorAsync(string trustedXUsername, CancellationToken ct);
    Task<Result> RemoveTrustedContributorAsync(string trustedXUsername, CancellationToken ct);

    Task<Result<List<PendingSubmission>>> GetPendingSubmissionsAsync(CancellationToken ct);
    Task<Result> ApproveSubmissionAsync(Guid folderId, Guid tweetId, CancellationToken ct);
    Task<Result> RejectSubmissionAsync(Guid folderId, Guid tweetId, CancellationToken ct);

    Task<Result<ContributionStats>> GetContributionStatsAsync(CancellationToken ct);
    Task<Result<ContributionStats>> GetFolderContributionStatsAsync(Guid folderId, CancellationToken ct);
}
