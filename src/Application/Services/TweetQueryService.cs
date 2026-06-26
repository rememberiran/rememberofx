using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class TweetQueryService : ITweetQueryService
{
    private readonly IAppDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<TweetQueryService> _logger;

    public TweetQueryService(IAppDbContext db, IBlobStorageService blobStorage, ILogger<TweetQueryService> logger)
    {
        _db = db;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<Result<TweetDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tweet = await _db.Tweets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tweet is null)
        {
            return Result.Failure<TweetDto>(DomainError.NotFound($"Tweet not found"));
        }

        var profile = tweet.AuthorXUserId != null
            ? await _db.XUserProfiles.FirstOrDefaultAsync(p => p.XUserId == tweet.AuthorXUserId, ct)
            : null;

        return Result.Success(MapToDto(tweet, profile));
    }

    public async Task<Result<TweetStatusDto>> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var tweet = await _db.Tweets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tweet is null)
        {
            return Result.Failure<TweetStatusDto>(DomainError.NotFound($"Tweet not found"));
        }

        object? tweetData = tweet.FetchStatus switch
        {
            "Ok" => MapToDto(tweet, await LoadAuthorProfile(tweet.AuthorXUserId, ct)),
            $"NotFound" or $"Private" or $"ScrapeFailed" => new { fetchStatus = tweet.FetchStatus, xTweetUrl = tweet.XTweetUrl },
            _ => null,
        };

        return Result.Success(new TweetStatusDto(tweet.Id, tweet.FetchStatus, tweetData));
    }

    public async Task<Result<SearchTweetsResult>> SearchAsync(SearchTweetsQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Q) && string.IsNullOrWhiteSpace(query.Tag) &&
            string.IsNullOrWhiteSpace(query.Username) && string.IsNullOrWhiteSpace(query.UserId))
        {
            return Result.Failure<SearchTweetsResult>(DomainError.Validation($"At least one search parameter (q, tag, username, userId) is required"));
        }

        IQueryable<TweetRecord> tweets;

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var searchTerm = "\"{query.Q}\"";
            tweets = _db.Tweets.FromSqlRaw(
                "SELECT DISTINCT t.* FROM Tweets t " +
                "LEFT JOIN XUserProfiles p ON t.AuthorXUserId = p.XUserId " +
                "WHERE t.FetchStatus = 'Ok' AND (" +
                "CONTAINS(t.TweetText, {0}) OR CONTAINS(p.CustomName, {0}) OR CONTAINS(p.Description, {0}))",
                searchTerm);
        }
        else
        {
            tweets = _db.Tweets.Where(t => t.FetchStatus == "Ok");
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tagSearch = "\"{query.Tag}\"";
            tweets = tweets.Where(t => t.Tags != null && t.Tags.Contains(tagSearch));
        }

        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            tweets = tweets.Where(t => t.AuthorXUsername != null &&
                EF.Functions.Like(t.AuthorXUsername, $"%{query.Username}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            tweets = tweets.Where(t => t.AuthorXUserId == query.UserId);
        }

        var totalCount = await tweets.CountAsync(ct);

        var sorted = string.Equals(query.Sort, $"date", StringComparison.Ordinal)
            ? tweets.OrderByDescending(t => t.CreatedAt)
            : tweets.OrderByDescending(t => t.VoteCount).ThenByDescending(t => t.CreatedAt);

        var page = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var authorIds = page
            .Where(t => t.AuthorXUserId != null)
            .Select(t => t.AuthorXUserId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var profiles = authorIds.Count > 0
            ? await _db.XUserProfiles
                .Where(p => authorIds.Contains(p.XUserId))
                .ToDictionaryAsync(p => p.XUserId, ct)
            : new Dictionary<string, XUserProfileRecord>(StringComparer.Ordinal);

        var items = page.Select(t => MapToDto(t, profiles.GetValueOrDefault(t.AuthorXUserId ?? string.Empty))).ToList();

        XUserProfileDto? subjectProfile = null;
        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            var profile = await _db.XUserProfiles.FirstOrDefaultAsync(p => p.XUserId == query.UserId, ct);
            if (profile != null)
            {
                subjectProfile = MapProfileToDto(profile);
            }
        }

        _logger.LogInformation("Search completed: {TotalCount} results for query {Query}", totalCount, query);

        return Result.Success(new SearchTweetsResult(items, totalCount, subjectProfile));
    }

    private async Task<XUserProfileRecord?> LoadAuthorProfile(string? authorXUserId, CancellationToken ct)
    {
        if (authorXUserId is null)
        {
            return null;
        }

        return await _db.XUserProfiles.FirstOrDefaultAsync(p => p.XUserId == authorXUserId, ct);
    }

    private TweetDto MapToDto(TweetRecord record, XUserProfileRecord? profile)
    {
        return new TweetDto(
            Id: record.Id,
            XTweetId: record.XTweetId,
            XTweetUrl: record.XTweetUrl,
            AuthorXUserId: record.AuthorXUserId,
            AuthorXUsername: record.AuthorXUsername,
            TweetText: record.TweetText,
            TweetDate: record.TweetDate,
            ScreenshotUrl: _blobStorage.GetScreenshotSasUrl(record.ScreenshotBlobName),
            Tags: record.Tags,
            VoteCount: record.VoteCount,
            FetchStatus: record.FetchStatus,
            CreatedAt: record.CreatedAt,
            AuthorProfile: profile != null ? MapProfileToDto(profile) : null);
    }

    private static XUserProfileDto MapProfileToDto(XUserProfileRecord record)
    {
        return new XUserProfileDto(record.Id, record.XUserId, record.ScrapedUsername, record.CustomName, record.Description);
    }
}
