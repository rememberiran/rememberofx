using Application.Models;

namespace Application.Interfaces;

public interface IPendingService
{
    Task<Result<List<PendingSubmission>>> GetPendingAdditionsAsync(CancellationToken ct);
    Task<Result> ApprovePendingAdditionAsync(Guid folderId, Guid tweetId, CancellationToken ct);
    Task<Result> RejectPendingAdditionAsync(Guid folderId, Guid tweetId, CancellationToken ct);
}
