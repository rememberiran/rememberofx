using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class ProfileOrchestrator
{
    private readonly ApiClient _apiClient;

    public ProfileOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ProfilePageModel?> GetAsync(
        string xUserId,
        string sort,
        int page,
        int pageSize)
    {
        var profileTask = _apiClient.GetXUserProfileAsync(xUserId);
        var tweetsTask = _apiClient.SearchTweetsAsync(
            q: null,
            tag: null,
            username: xUserId,
            sort: sort,
            page: page,
            pageSize: pageSize);

        await Task.WhenAll(profileTask, tweetsTask);

        var profile = await profileTask;
        if (profile is null)
        {
            return null;
        }

        var tweetsResult = await tweetsTask;
        var totalCount = tweetsResult?.TotalCount ?? 0;
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        var totalVotes = tweetsResult?.Items.Sum(t => t.VoteCount) ?? 0;

        var tweets = tweetsResult?.Items
            .Select(ModelMappers.ToTweetViewModel)
            .ToList() ?? [];

        var profileVm = ModelMappers.ToProfileViewModel(profile, totalCount, totalVotes);

        return new ProfilePageModel(profileVm, tweets, totalCount, page, totalPages, sort);
    }
}
