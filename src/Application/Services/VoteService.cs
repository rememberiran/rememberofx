using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class VoteService : IVoteService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<VoteService> _logger;

    public VoteService(IAppDbContext db, ILogger<VoteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> CastVoteAsync(Guid tweetId, string voterIp, Guid? voterUserId, CancellationToken ct)
    {
        var tweet = await _db.Tweets.FirstOrDefaultAsync(t => t.Id == tweetId, ct);
        if (tweet is null)
        {
            return Result.Failure(DomainError.NotFound($"Tweet not found"));
        }

        if (voterUserId.HasValue)
        {
            var existingByUser = await _db.Votes
                .AnyAsync(v => v.TweetId == tweetId && v.VoterUserId == voterUserId, ct);
            if (existingByUser)
            {
                return Result.Failure(DomainError.Conflict($"Already voted"));
            }

            // Auth overrides anonymous: if same user voted anonymously from same IP, don't increment
            var anonymousVoteFromSameIp = await _db.Votes
                .AnyAsync(v => v.TweetId == tweetId && v.VoterIp == voterIp && v.VoterUserId == null, ct);
            if (anonymousVoteFromSameIp)
            {
                // Record the authenticated vote but don't increment count
                _db.Votes.Add(new VoteRecord
                {
                    Id = Guid.NewGuid(),
                    TweetId = tweetId,
                    VoterIp = voterIp,
                    VoterUserId = voterUserId,
                });
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Authenticated vote recorded for tweet {TweetId} by user {UserId} (count not incremented, anonymous vote exists from same IP)", tweetId, voterUserId);
                return Result.Success();
            }
        }
        else
        {
            var existingByIp = await _db.Votes
                .AnyAsync(v => v.TweetId == tweetId && v.VoterIp == voterIp, ct);
            if (existingByIp)
            {
                return Result.Failure(DomainError.Conflict($"Already voted"));
            }
        }

        _db.Votes.Add(new VoteRecord
        {
            Id = Guid.NewGuid(),
            TweetId = tweetId,
            VoterIp = voterIp,
            VoterUserId = voterUserId,
        });

        await _db.Database.ExecuteSqlRawAsync(
$"UPDATE Tweets SET VoteCount = VoteCount + 1 WHERE Id = {0}", tweetId, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Vote cast for tweet {TweetId} by {VoterIdentity}", tweetId, voterUserId?.ToString() ?? voterIp);
        return Result.Success();
    }
}
