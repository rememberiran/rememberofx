using Domain.Entities;
using Storage;

namespace Domain.Mappers;

public static class VoteMapper
{
    public static Vote ToDomain(VoteRecord record)
    {
        return new Vote
        {
            Id = record.Id,
            TweetId = record.TweetId,
            VoterIp = record.VoterIp,
            VoterUserId = record.VoterUserId,
            CreatedAt = record.CreatedAt,
            Tweet = TweetMapper.ToDomain(record.Tweet),
            VoterUser = record.VoterUser is not null ? UserMapper.ToDomain(record.VoterUser) : null,
        };
    }

    public static VoteRecord ToRecord(Vote entity)
    {
        return new VoteRecord
        {
            Id = entity.Id,
            TweetId = entity.TweetId,
            VoterIp = entity.VoterIp,
            VoterUserId = entity.VoterUserId,
            CreatedAt = entity.CreatedAt,
        };
    }
}
