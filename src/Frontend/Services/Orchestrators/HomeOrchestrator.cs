using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class HomeOrchestrator
{
    private readonly ApiClient _apiClient;

    public HomeOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<HomePageModel> GetAsync()
    {
        var foldersTask = _apiClient.GetRootFoldersAsync();
        var tweetsTask = _apiClient.SearchTweetsAsync(
            q: null,
            tag: null,
            username: null,
            sort: "votes",
            page: 1,
            pageSize: 4);

        await Task.WhenAll(foldersTask, tweetsTask);

        var foldersResult = await foldersTask;
        var tweetsResult = await tweetsTask;

        var folders = foldersResult?
            .Select(ModelMappers.ToFolderViewModel)
            .ToList() ?? [];

        var topTweets = tweetsResult?.Items
            .Select(ModelMappers.ToTweetViewModel)
            .ToList() ?? [];

        return new HomePageModel(folders, topTweets);
    }
}
