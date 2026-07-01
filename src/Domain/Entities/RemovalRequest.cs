namespace Domain.Entities;

public class RemovalRequest
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public Guid TweetId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string RequestedByIp { get; set; } = default!;
    public DateTime RequestedAt { get; set; }
    public string Status { get; set; } = default!;
    public DateTime? ResolvedAt { get; set; }

    public string FolderName { get; set; } = default!;
    public string TweetXId { get; set; } = default!;
    public IReadOnlyList<RemovalApproval> Approvals { get; set; } = [];
}

public class RemovalApproval
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public string ApprovedByXUsername { get; set; } = default!;
    public DateTime ApprovedAt { get; set; }
    public bool IsVoid { get; set; }
}
