using Domain.Entities;

namespace Application.Interfaces;

public interface IRemovalRequestService
{
    Task<Result<RemovalRequest>> SubmitAsync(Guid folderId, Guid tweetId, CancellationToken ct);
    Task<Result<RemovalRequest>> ApproveAsync(Guid requestId, CancellationToken ct);
    Task<Result<RemovalRequest>> RejectAsync(Guid requestId, CancellationToken ct);
    Task<Result<List<RemovalRequest>>> GetPendingAsync(CancellationToken ct);
}
