using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class SearchService : ISearchService
{
    private readonly IAppDbContext _db;
    private static readonly EventId SearchCompletedEvent = new(1040, "SearchCompleted");

    private readonly ILogger<SearchService> _logger;

    public SearchService(IAppDbContext db, ILogger<SearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<TweetSearchResult>> SearchAsync(SearchTweetsQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Q) && string.IsNullOrWhiteSpace(query.Tag) &&
            string.IsNullOrWhiteSpace(query.Username) && string.IsNullOrWhiteSpace(query.UserId) &&
            !query.SubmittedByUserId.HasValue)
        {
            return Result.Failure<TweetSearchResult>(DomainError.Validation($"At least one search parameter (q, tag, username, userId, submittedByUserId) is required"));
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
                searchTerm)
                .Include(t => t.SubmittedByUser)
                .Include(t => t.FolderTweets)
                    .ThenInclude(ft => ft.Folder)
                .AsNoTracking();
        }
        else
        {
            tweets = _db.Tweets
                .Include(t => t.SubmittedByUser)
                .Include(t => t.FolderTweets)
                    .ThenInclude(ft => ft.Folder)
                .AsNoTracking()
                .Where(t => t.FetchStatus == "Ok");
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

        if (query.SubmittedByUserId.HasValue)
        {
            tweets = tweets.Where(t => t.SubmittedByUserId == query.SubmittedByUserId);
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
            ? await _db.XUserProfiles.AsNoTracking()
                .Where(p => authorIds.Contains(p.XUserId))
                .ToDictionaryAsync(p => p.XUserId, ct)
            : new Dictionary<string, XUserProfileRecord>(StringComparer.Ordinal);

        var items = page.Select(t =>
        {
            var authorProfile = profiles.GetValueOrDefault(t.AuthorXUserId ?? string.Empty);
            return new TweetWithAuthor(
                TweetMapper.ToDomain(t),
                authorProfile != null ? XUserProfileMapper.ToDomain(authorProfile) : null);
        }).ToList();

        XUserProfile? subjectProfile = null;
        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            var profile = await _db.XUserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.XUserId == query.UserId, ct);
            if (profile != null)
            {
                subjectProfile = XUserProfileMapper.ToDomain(profile);
            }
        }

        _logger.LogInformation(SearchCompletedEvent, "Search completed: {TotalCount} results for query {Query}", totalCount, query);

        return Result.Success(new TweetSearchResult(items, totalCount, subjectProfile));
    }
}
