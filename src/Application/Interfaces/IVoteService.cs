namespace Application.Interfaces;

public interface IVoteService
{
    Task<Result> CastVoteAsync(Guid tweetId, Guid? voterUserId, CancellationToken ct);
}
