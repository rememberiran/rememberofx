using Domain.Entities;
using Domain.Enums;
using Storage;

namespace Domain.Mappers;

public static class TweetMediaMapper
{
    public static TweetMedia ToDomain(TweetMediaRecord record)
    {
        return new TweetMedia
        {
            Id = record.Id,
            TweetId = record.TweetId,
            MediaType = Enum.Parse<MediaType>(record.MediaType),
            BlobName = record.BlobName,
            OriginalUrl = record.OriginalUrl,
            OrderIndex = record.OrderIndex,
            CreatedAt = record.CreatedAt,
        };
    }

    public static TweetMediaRecord ToRecord(TweetMedia entity)
    {
        return new TweetMediaRecord
        {
            Id = entity.Id,
            TweetId = entity.TweetId,
            MediaType = entity.MediaType.ToString(),
            BlobName = entity.BlobName,
            OriginalUrl = entity.OriginalUrl,
            OrderIndex = entity.OrderIndex,
            CreatedAt = entity.CreatedAt,
        };
    }
}
