namespace Storage;

public class FolderTweetRemovalApprovalRecord
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
    public bool IsVoid { get; set; }

    public FolderTweetRemovalRequestRecord Request { get; set; } = default!;
    public UserRecord ApprovedByUser { get; set; } = default!;
}
