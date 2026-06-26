namespace Application.Interfaces;

public interface IVoteService
{
    Task<Result> CastVoteAsync(Guid tweetId, string voterIp, Guid? voterUserId, CancellationToken ct);
}
