using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class VoteService : IVoteService
{
    private readonly IAppDbContext _db;
    private static readonly EventId VoteCastEvent = new(1030, "VoteCast");

    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly ILogger<VoteService> _logger;

    public VoteService(IAppDbContext db, IAsyncContext<IdentityContext> identityContext, ILogger<VoteService> logger)
    {
        _db = db;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task<Result> CastVoteAsync(Guid tweetId, Guid? voterUserId, CancellationToken ct)
    {
        var voterIp = _identityContext.Value!.IpAddress;
        var tweet = await _db.Tweets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tweetId, ct);
        if (tweet is null)
        {
            return Result.Failure(DomainError.NotFound($"Tweet not found"));
        }

        var incrementCount = true;

        if (voterUserId.HasValue)
        {
            var existingByUser = await _db.Votes
                .AnyAsync(v => v.TweetId == tweetId && v.VoterUserId == voterUserId, ct);
            if (existingByUser)
            {
                return Result.Failure(DomainError.Conflict($"Already voted"));
            }

            var anonymousVoteFromSameIp = await _db.Votes
                .AnyAsync(v => v.TweetId == tweetId && v.VoterIp == voterIp && v.VoterUserId == null, ct);
            if (anonymousVoteFromSameIp)
            {
                incrementCount = false;
            }
        }

        _db.Votes.Add(new VoteRecord
        {
            Id = Guid.NewGuid(),
            TweetId = tweetId,
            VoterIp = voterIp,
            VoterUserId = voterUserId,
        });

        try
        {
            // Explicit save + transaction: the unique index on (TweetId, VoterIp) prevents
            // duplicate votes at the DB level, avoiding the check-then-act race condition.
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.SaveChangesAsync(ct);

            if (incrementCount)
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Tweets SET VoteCount = VoteCount + 1 WHERE Id = {0}",
                    tweetId,
                    ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return Result.Failure(DomainError.Conflict($"Already voted"));
        }

        _logger.LogInformation(VoteCastEvent, "Vote cast for tweet {TweetId} by {VoterIdentity}", tweetId, voterUserId?.ToString() ?? voterIp);
        return Result.Success();
    }
}
