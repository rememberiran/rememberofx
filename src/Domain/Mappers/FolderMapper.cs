using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class FolderMapper
{
    public static Folder ToDomain(FolderRecord record)
    {
        return new Folder
        {
            Id = record.Id,
            ParentFolderId = record.ParentFolderId,
            Name = record.Name,
            Description = record.Description,
            CreatedByUserId = record.CreatedByUserId,
            CreatedAt = record.CreatedAt,
            IsActive = record.IsActive,
            ParentFolder = record.ParentFolder is not null ? ToDomain(record.ParentFolder) : null,
            CreatedByUser = UserMapper.ToDomain(record.CreatedByUser),
            Children = record.Children.Select(ToDomain).ToList(),
            FolderTweets = record.FolderTweets.Select(FolderTweetMapper.ToDomain).ToList(),
        };
    }

    public static FolderRecord ToRecord(Folder entity)
    {
        return new FolderRecord
        {
            Id = entity.Id,
            ParentFolderId = entity.ParentFolderId,
            Name = entity.Name,
            Description = entity.Description,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedAt = entity.CreatedAt,
            IsActive = entity.IsActive,
        };
    }
}
