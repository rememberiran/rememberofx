using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class AdminOrchestrator
{
    private readonly ApiClient _apiClient;

    public AdminOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<AdminPageModel> GetAsync()
    {
        var users = await _apiClient.GetUsersAsync();

        var mapped = users?
            .Select(ModelMappers.ToUserViewModel)
            .ToList() ?? [];

        return new AdminPageModel(mapped);
    }
}
