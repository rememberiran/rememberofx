namespace Storage;

public class FolderTweetRemovalRequestRecord
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public Guid TweetId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string RequestedByIp { get; set; } = default!;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending";
    public DateTime? ResolvedAt { get; set; }

    public FolderRecord Folder { get; set; } = default!;
    public TweetRecord Tweet { get; set; } = default!;
    public UserRecord? RequestedByUser { get; set; }
    public ICollection<FolderTweetRemovalApprovalRecord> Approvals { get; set; } = [];
}
