using System.Globalization;
using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class MyArchiveOrchestrator
{
    private readonly ApiClient _apiClient;

    public MyArchiveOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<MyArchivePageModel?> GetAsync()
    {
        var statsTask = _apiClient.GetMyStatsAsync();
        var tweetsTask = _apiClient.GetMyTweetsAsync();
        var foldersTask = _apiClient.GetMyFoldersAsync();

        await Task.WhenAll(statsTask, tweetsTask, foldersTask);

        var stats = await statsTask;
        var tweetsResult = await tweetsTask;
        var folders = await foldersTask;

        var tweetViewModels = tweetsResult?.Items
            .Select(t => ModelMappers.ToTweetViewModel(t) with
            {
                SubmittedDate = t.CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
            })
            .ToList() ?? [];

        var folderViewModels = folders?
            .Select(ModelMappers.ToMyFolderViewModel)
            .ToList() ?? [];

        return new MyArchivePageModel(
            SubmittedTweets: tweetViewModels,
            CreatedFolders: folderViewModels,
            TotalVotesEarned: stats?.TotalVotesEarned ?? 0,
            DeletedTweetsPreserved: stats?.DeletedTweetsPreserved ?? 0);
    }
}
