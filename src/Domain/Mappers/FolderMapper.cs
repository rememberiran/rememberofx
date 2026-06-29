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
            Icon = record.Icon,
            CreatedByUserId = record.CreatedByUserId,
            CreatedAt = record.CreatedAt,
            IsActive = record.IsActive,
            Visibility = record.Visibility,
            ParentFolder = record.ParentFolder is not null ? ToDomain(record.ParentFolder) : null,
            CreatedByUser = record.CreatedByUser is not null ? UserMapper.ToDomain(record.CreatedByUser) : null!,
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
            Icon = entity.Icon,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedAt = entity.CreatedAt,
            IsActive = entity.IsActive,
            Visibility = entity.Visibility,
        };
    }
}
