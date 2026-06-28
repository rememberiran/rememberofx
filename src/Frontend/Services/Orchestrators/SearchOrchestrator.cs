using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class SearchOrchestrator
{
    private readonly ApiClient _apiClient;

    public SearchOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<SearchPageModel> GetAsync(
        string? q,
        string? tag,
        string? username,
        string sort,
        int page,
        int pageSize)
    {
        var result = await _apiClient.SearchTweetsAsync(q, tag, username, sort, page, pageSize);

        var totalCount = result?.TotalCount ?? 0;
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        var tweets = result?.Items
            .Select(ModelMappers.ToTweetViewModel)
            .ToList() ?? [];

        ProfileViewModel? subjectProfile = null;
        if (result?.SubjectProfile is not null)
        {
            var items = result.Items;
            var totalVotes = items.Sum(t => t.VoteCount);
            subjectProfile = ModelMappers.ToProfileViewModel(
                result.SubjectProfile,
                totalCount,
                totalVotes);
        }

        return new SearchPageModel(
            tweets,
            totalCount,
            page,
            totalPages,
            q,
            tag,
            username,
            sort,
            subjectProfile);
    }
}
