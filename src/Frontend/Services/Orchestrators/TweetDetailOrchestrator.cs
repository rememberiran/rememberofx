using Frontend.Models;

namespace Frontend.Services.Orchestrators;

public class TweetDetailOrchestrator
{
    private readonly ApiClient _apiClient;

    public TweetDetailOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<TweetDetailPageModel?> GetAsync(Guid tweetId)
    {
        var tweet = await _apiClient.GetTweetAsync(tweetId);
        if (tweet is null)
        {
            return null;
        }

        ProfileViewModel? authorProfile = null;
        if (!string.IsNullOrEmpty(tweet.AuthorXUserId))
        {
            var profile = await _apiClient.GetXUserProfileAsync(tweet.AuthorXUserId);
            if (profile is not null)
            {
                authorProfile = ModelMappers.ToProfileViewModel(profile);
            }
        }

        var tweetVm = ModelMappers.ToTweetViewModel(tweet);

        var breadcrumb = new List<BreadcrumbItemViewModel>
        {
            new("Home", "/"),
            new($"@{tweetVm.Author}", null),
        };

        CaptureInfoViewModel? capture = null;

        return new TweetDetailPageModel(tweetVm, breadcrumb, authorProfile, capture);
    }
}
