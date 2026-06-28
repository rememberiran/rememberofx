using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class MyArchiveOrchestrator
{
    public Task<MyArchivePageModel?> GetAsync()
    {
        var model = new MyArchivePageModel(
            SubmittedTweets: [],
            CreatedFolders: [],
            TotalVotesEarned: 0,
            DeletedTweetsPreserved: 0);

        return Task.FromResult<MyArchivePageModel?>(model);
    }
}
