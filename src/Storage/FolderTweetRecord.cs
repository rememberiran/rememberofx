namespace Storage;

public class FolderTweetRecord
{
    public Guid FolderId { get; set; }
    public Guid TweetId { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public FolderRecord Folder { get; set; } = default!;
    public TweetRecord Tweet { get; set; } = default!;
    public UserRecord AddedByUser { get; set; } = default!;
}
