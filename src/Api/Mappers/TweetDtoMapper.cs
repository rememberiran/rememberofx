using Application.Interfaces;
using Application.Models;
using Domain.Entities;

namespace Api.Mappers;

public class TweetDtoMapper
{
    private readonly IBlobStorageService _blobStorage;

    public TweetDtoMapper(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public TweetDto ToDto(Tweet tweet, XUserProfile? authorProfile = null)
    {
        return new TweetDto(
            tweet.Id,
            tweet.XTweetId,
            tweet.XTweetUrl,
            tweet.AuthorXUserId,
            tweet.AuthorXUsername,
            tweet.TweetText,
            tweet.TweetDate,
            _blobStorage.GetScreenshotSasUrl(tweet.ScreenshotBlobName),
            tweet.Tags,
            tweet.VoteCount,
            tweet.FetchStatus.ToString(),
            tweet.CreatedAt,
            authorProfile != null ? XUserProfileDtoMapper.ToDto(authorProfile) : null);
    }

    public TweetDto ToDto(TweetWithAuthor tweetWithAuthor)
    {
        return ToDto(tweetWithAuthor.Tweet, tweetWithAuthor.AuthorProfile);
    }

    public IReadOnlyList<TweetDto> ToDtoList(IEnumerable<TweetWithAuthor> items)
    {
        return items.Select(ToDto).ToList();
    }
}
