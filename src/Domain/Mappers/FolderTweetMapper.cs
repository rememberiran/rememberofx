using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class FolderTweetMapper
{
    public static FolderTweet ToDomain(FolderTweetRecord record)
    {
        return new FolderTweet
        {
            FolderId = record.FolderId,
            TweetId = record.TweetId,
            AddedByUserId = record.AddedByUserId,
            AddedAt = record.AddedAt,
            Folder = record.Folder is not null ? FolderMapper.ToDomain(record.Folder) : null!,
            Tweet = record.Tweet is not null ? TweetMapper.ToDomain(record.Tweet) : null!,
            AddedByUser = record.AddedByUser is not null ? UserMapper.ToDomain(record.AddedByUser) : null!,
        };
    }

    public static FolderTweetRecord ToRecord(FolderTweet entity)
    {
        return new FolderTweetRecord
        {
            FolderId = entity.FolderId,
            TweetId = entity.TweetId,
            AddedByUserId = entity.AddedByUserId,
            AddedAt = entity.AddedAt,
        };
    }
}
