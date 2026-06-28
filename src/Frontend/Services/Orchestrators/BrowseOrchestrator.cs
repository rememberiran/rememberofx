using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class BrowseOrchestrator
{
    private readonly ApiClient _apiClient;

    public BrowseOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<BrowsePageModel> GetAsync()
    {
        var folders = await _apiClient.GetRootFoldersAsync();

        var mapped = folders?
            .Select(ModelMappers.ToFolderViewModel)
            .ToList() ?? [];

        return new BrowsePageModel(mapped);
    }
}
