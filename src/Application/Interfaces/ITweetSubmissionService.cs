using Application.Models;

namespace Application.Interfaces;

public interface ITweetSubmissionService
{
    Task<Result<TweetSubmissionResult>> SubmitAsync(SubmitTweetCommand command, CancellationToken ct);
}

public record TweetSubmissionResult(Guid TweetId, string FetchStatus);
