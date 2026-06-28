using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class SubmitOrchestrator
{
    private readonly ApiClient _apiClient;

    public SubmitOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<SubmitPageModel> GetAsync()
    {
        var folders = await _apiClient.GetRootFoldersAsync();

        var options = folders?
            .Select(f => new FolderOptionViewModel(f.Id, f.Name, f.TweetCount))
            .ToList() ?? [];

        return new SubmitPageModel(options);
    }
}
