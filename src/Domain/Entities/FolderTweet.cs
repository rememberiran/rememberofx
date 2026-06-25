namespace Domain.Entities;

public class FolderTweet
{
    public Guid FolderId { get; set; }
    public Guid TweetId { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Folder Folder { get; set; } = default!;
    public Tweet Tweet { get; set; } = default!;
    public User AddedByUser { get; set; } = default!;
}
