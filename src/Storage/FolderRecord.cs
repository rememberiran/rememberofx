namespace Storage;

public class FolderRecord
{
    public Guid Id { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public FolderRecord? ParentFolder { get; set; }
    public UserRecord CreatedByUser { get; set; } = default!;
    public ICollection<FolderRecord> Children { get; set; } = new List<FolderRecord>();
    public ICollection<FolderTweetRecord> FolderTweets { get; set; } = new List<FolderTweetRecord>();
}
