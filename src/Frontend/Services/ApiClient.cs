using System.Net.Http.Json;
using Application.Models;
using Microsoft.Extensions.Logging;

namespace Frontend.Services;

public record ApiPagedResponse<T>(IReadOnlyList<T> Items, int TotalCount);

public record SearchTweetsApiResponse(
    IReadOnlyList<TweetDto> Items,
    int TotalCount,
    XUserProfileDto? SubjectProfile);

public record SubmitTweetApiResponse(Guid TweetId, string FetchStatus);

public record TweetStatusApiResponse(Guid TweetId, string FetchStatus);

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FolderSummaryDto>?> GetRootFoldersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FolderSummaryDto[]>("api/folders");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get root folders");
            return null;
        }
    }

    public async Task<FolderDto?> GetFolderAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FolderDto>($"api/folders/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get folder {FolderId}", id);
            return null;
        }
    }

    public async Task<ApiPagedResponse<TweetDto>?> GetFolderTweetsAsync(
        Guid folderId,
        string sort,
        int page,
        int pageSize)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApiPagedResponse<TweetDto>>(
                $"api/folders/{folderId}/tweets?sort={sort}&page={page}&pageSize={pageSize}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get folder tweets for {FolderId}", folderId);
            return null;
        }
    }

    public async Task<TweetDto?> GetTweetAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TweetDto>($"api/tweets/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tweet {TweetId}", id);
            return null;
        }
    }

    public async Task<SearchTweetsApiResponse?> SearchTweetsAsync(
        string? q,
        string? tag,
        string? username,
        string sort,
        int page,
        int pageSize)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"sort={Uri.EscapeDataString(sort)}",
                $"page={page}",
                $"pageSize={pageSize}",
            };

            if (!string.IsNullOrWhiteSpace(q))
            {
                queryParams.Add($"q={Uri.EscapeDataString(q)}");
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                queryParams.Add($"tag={Uri.EscapeDataString(tag)}");
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                queryParams.Add($"username={Uri.EscapeDataString(username)}");
            }

            var url = $"api/search?{string.Join("&", queryParams)}";
            return await _httpClient.GetFromJsonAsync<SearchTweetsApiResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search tweets");
            return null;
        }
    }

    public async Task<SubmitTweetApiResponse?> SubmitTweetAsync(string tweetUrl, IReadOnlyList<Guid>? folderIds)
    {
        try
        {
            var body = new { TweetUrl = tweetUrl, FolderIds = folderIds };
            var response = await _httpClient.PostAsJsonAsync("api/tweets", body);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SubmitTweetApiResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit tweet");
            return null;
        }
    }

    public async Task<TweetStatusApiResponse?> GetTweetStatusAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TweetStatusApiResponse>($"api/tweets/{id}/status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tweet status {TweetId}", id);
            return null;
        }
    }

    public async Task<bool> CastVoteAsync(Guid tweetId)
    {
        try
        {
            var response = await _httpClient.PostAsync(new Uri($"api/votes/{tweetId}", UriKind.Relative), null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cast vote for tweet {TweetId}", tweetId);
            return false;
        }
    }

    public async Task<IReadOnlyList<UserDto>?> GetUsersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserDto[]>("api/admin/users");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users");
            return null;
        }
    }

    public async Task<UserDto?> AddUserAsync(string xUserId, string role)
    {
        try
        {
            var body = new { XUserId = xUserId, Role = role };
            var response = await _httpClient.PostAsJsonAsync("api/admin/users", body);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user");
            return null;
        }
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, string? role, bool? isActive)
    {
        try
        {
            var body = new { Role = role, IsActive = isActive };
            var response = await _httpClient.PutAsJsonAsync($"api/admin/users/{id}", body);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", id);
            return null;
        }
    }

    public async Task<bool> DeactivateUserAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(new Uri($"api/admin/users/{id}", UriKind.Relative));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate user {UserId}", id);
            return false;
        }
    }

    public async Task<XUserProfileDto?> GetXUserProfileAsync(string xUserId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<XUserProfileDto>(
                $"api/xusers/{Uri.EscapeDataString(xUserId)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get X user profile {XUserId}", xUserId);
            return null;
        }
    }

    public async Task<FolderDto?> CreateFolderAsync(string name, string? description, Guid? parentFolderId)
    {
        try
        {
            var body = new { Name = name, Description = description, ParentFolderId = parentFolderId };
            var response = await _httpClient.PostAsJsonAsync("api/folders", body);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FolderDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder");
            return null;
        }
    }

    public async Task<bool> AddTweetToFolderAsync(Guid folderId, Guid tweetId)
    {
        try
        {
            var response = await _httpClient.PostAsync(new Uri($"api/folders/{folderId}/tweets/{tweetId}", UriKind.Relative), null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add tweet {TweetId} to folder {FolderId}", tweetId, folderId);
            return false;
        }
    }

    public async Task<bool> RemoveTweetFromFolderAsync(Guid folderId, Guid tweetId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(new Uri($"api/folders/{folderId}/tweets/{tweetId}", UriKind.Relative));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tweet {TweetId} from folder {FolderId}", tweetId, folderId);
            return false;
        }
    }
}
