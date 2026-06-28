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

    public TweetDto ToDto(Tweet tweet, XUserProfile? authorProfile = null, IReadOnlySet<Guid>? votedTweetIds = null)
    {
        var media = tweet.Media
            .OrderBy(m => m.OrderIndex)
            .Select(m => new TweetMediaDto(
                m.Id,
                m.MediaType.ToString(),
                _blobStorage.GetMediaSasUrl(m.BlobName),
                m.OrderIndex))
            .ToList();

        var folders = tweet.FolderTweets
            .Select(ft => new TweetFolderDto(ft.FolderId, ft.Folder.Name))
            .ToList();

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
            media,
            authorProfile != null ? XUserProfileDtoMapper.ToDto(authorProfile) : null,
            folders,
            SubmittedByUsername: tweet.SubmittedByUser?.XUsername,
            IsVotedByMe: votedTweetIds?.Contains(tweet.Id) ?? false);
    }

    public TweetDto ToDto(TweetWithAuthor tweetWithAuthor, IReadOnlySet<Guid>? votedTweetIds = null)
    {
        return ToDto(tweetWithAuthor.Tweet, tweetWithAuthor.AuthorProfile, votedTweetIds);
    }

    public IReadOnlyList<TweetDto> ToDtoList(IEnumerable<TweetWithAuthor> items, IReadOnlySet<Guid>? votedTweetIds = null)
    {
        return items.Select(i => ToDto(i, votedTweetIds)).ToList();
    }
}
