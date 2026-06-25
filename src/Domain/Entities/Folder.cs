namespace Domain.Entities;

public class Folder
{
    public Guid Id { get; set; }
    public Guid? ParentFolderId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public Folder? ParentFolder { get; set; }
    public User CreatedByUser { get; set; } = default!;
    public ICollection<Folder> Children { get; set; } = new List<Folder>();
    public ICollection<FolderTweet> FolderTweets { get; set; } = new List<FolderTweet>();
}
