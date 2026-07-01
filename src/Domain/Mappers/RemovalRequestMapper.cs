using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class RemovalRequestMapper
{
    public static RemovalRequest ToDomain(FolderTweetRemovalRequestRecord record)
    {
        return new RemovalRequest
        {
            Id = record.Id,
            FolderId = record.FolderId,
            TweetId = record.TweetId,
            RequestedByUserId = record.RequestedByUserId,
            RequestedByIp = record.RequestedByIp,
            RequestedAt = record.RequestedAt,
            Status = record.Status,
            ResolvedAt = record.ResolvedAt,
            FolderName = record.Folder?.Name ?? string.Empty,
            TweetXId = record.Tweet?.XTweetId ?? string.Empty,
            Approvals = record.Approvals
                .Select(a => new RemovalApproval
                {
                    Id = a.Id,
                    RequestId = a.RequestId,
                    ApprovedByUserId = a.ApprovedByUserId,
                    ApprovedByXUsername = a.ApprovedByUser?.XUsername ?? string.Empty,
                    ApprovedAt = a.ApprovedAt,
                    IsVoid = a.IsVoid,
                })
                .ToList(),
        };
    }
}
