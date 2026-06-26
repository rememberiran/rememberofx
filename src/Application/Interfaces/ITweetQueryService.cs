using Application.Models;

namespace Application.Interfaces;

public interface ITweetQueryService
{
    Task<Result<TweetDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<TweetStatusDto>> GetStatusAsync(Guid id, CancellationToken ct);
    Task<Result<SearchTweetsResult>> SearchAsync(SearchTweetsQuery query, CancellationToken ct);
}
