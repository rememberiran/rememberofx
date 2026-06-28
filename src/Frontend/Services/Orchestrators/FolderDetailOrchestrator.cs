using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class FolderDetailOrchestrator
{
    private readonly ApiClient _apiClient;

    public FolderDetailOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<FolderDetailPageModel?> GetAsync(
        Guid folderId,
        string sort,
        int page,
        int pageSize)
    {
        var folderTask = _apiClient.GetFolderAsync(folderId);
        var tweetsTask = _apiClient.GetFolderTweetsAsync(folderId, sort, page, pageSize);

        await Task.WhenAll(folderTask, tweetsTask);

        var folder = await folderTask;
        if (folder is null)
        {
            return null;
        }

        var tweetsResult = await tweetsTask;
        var totalCount = tweetsResult?.TotalCount ?? 0;
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        var breadcrumb = (folder.Breadcrumb ?? [])
            .Select(b => new BreadcrumbItemViewModel(b.Name, $"/folders/{b.Id}"))
            .Append(new BreadcrumbItemViewModel(folder.Name, null))
            .ToList();

        var subfolders = (folder.Children ?? [])
            .Select(c => new SubfolderViewModel(c.Id, c.Name, c.TweetCount))
            .ToList();

        var tweets = tweetsResult?.Items
            .Select(ModelMappers.ToTweetViewModel)
            .ToList() ?? [];

        var folderVm = new FolderViewModel(
            folder.Id,
            folder.Name,
            folder.Description,
            ModelMappers.GetFolderIcon(folder.Name),
            folder.Children?.Count ?? 0,
            totalCount);

        return new FolderDetailPageModel(
            folderVm,
            breadcrumb,
            subfolders,
            tweets,
            totalCount,
            page,
            totalPages,
            sort);
    }
}
