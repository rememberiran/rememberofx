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
            Folder = FolderMapper.ToDomain(record.Folder),
            Tweet = TweetMapper.ToDomain(record.Tweet),
            AddedByUser = UserMapper.ToDomain(record.AddedByUser),
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
