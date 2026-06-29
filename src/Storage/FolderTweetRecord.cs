namespace Storage;

public class FolderTweetRecord
{
    public Guid FolderId { get; set; }
    public Guid TweetId { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "approved";
    public DateTime? ReviewedAt { get; set; }

    public FolderRecord Folder { get; set; } = default!;
    public TweetRecord Tweet { get; set; } = default!;
    public UserRecord AddedByUser { get; set; } = default!;
}
