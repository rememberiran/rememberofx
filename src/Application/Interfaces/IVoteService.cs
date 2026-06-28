namespace Application.Interfaces;

public interface IVoteService
{
    Task<Result> CastVoteAsync(Guid tweetId, Guid? voterUserId, CancellationToken ct);

    Task<Result<HashSet<Guid>>> GetVotedTweetIdsAsync(Guid userId, IReadOnlyList<Guid> tweetIds, CancellationToken ct);
}
