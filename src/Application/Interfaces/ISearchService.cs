using Application.Models;

namespace Application.Interfaces;

public interface ISearchService
{
    Task<Result<TweetSearchResult>> SearchAsync(SearchTweetsQuery query, CancellationToken ct);
}
