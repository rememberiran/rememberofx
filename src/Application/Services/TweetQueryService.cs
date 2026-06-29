using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class TweetQueryService : ITweetQueryService
{
    private static readonly EventId SubmitterStatsRetrievedEvent = new(1070, "SubmitterStatsRetrieved");

    private readonly IAppDbContext _db;
    private readonly ILogger<TweetQueryService> _logger;

    public TweetQueryService(IAppDbContext db, ILogger<TweetQueryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<TweetWithAuthor>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tweet = await _db.Tweets.AsNoTracking()
            .Include(t => t.SubmittedByUser)
            .Include(t => t.FolderTweets)
                .ThenInclude(ft => ft.Folder)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tweet is null)
        {
            return Result.Failure<TweetWithAuthor>(DomainError.NotFound($"Tweet not found"));
        }

        var profile = tweet.AuthorXUserId != null
            ? await _db.XUserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.XUserId == tweet.AuthorXUserId, ct)
            : null;

        return Result.Success(new TweetWithAuthor(
            TweetMapper.ToDomain(tweet),
            profile != null ? XUserProfileMapper.ToDomain(profile) : null));
    }

    public async Task<Result<Tweet>> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var tweet = await _db.Tweets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tweet is null)
        {
            return Result.Failure<Tweet>(DomainError.NotFound($"Tweet not found"));
        }

        return Result.Success(TweetMapper.ToDomain(tweet));
    }

    public async Task<Result<SubmitterStatsDto>> GetSubmitterStatsAsync(Guid userId, CancellationToken ct)
    {
        var tweetStats = await _db.Tweets.AsNoTracking()
            .Where(t => t.SubmittedByUserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                SubmittedTweetCount = g.Count(),
                TotalVotesEarned = g.Sum(t => t.VoteCount),
                DeletedTweetsPreserved = g.Count(t => t.FetchStatus != "Ok"),
            })
            .FirstOrDefaultAsync(ct);

        var folderCount = await _db.Folders.AsNoTracking()
            .CountAsync(f => f.CreatedByUserId == userId && f.IsActive, ct);

        var dto = new SubmitterStatsDto(
            SubmittedTweetCount: tweetStats?.SubmittedTweetCount ?? 0,
            TotalVotesEarned: tweetStats?.TotalVotesEarned ?? 0,
            DeletedTweetsPreserved: tweetStats?.DeletedTweetsPreserved ?? 0,
            CreatedFolderCount: folderCount);

        _logger.LogInformation(
            SubmitterStatsRetrievedEvent,
            "Submitter stats retrieved for user {UserId}: {SubmittedCount} tweets, {VotesEarned} votes, {DeletedPreserved} deleted preserved, {FolderCount} folders",
            userId,
            dto.SubmittedTweetCount,
            dto.TotalVotesEarned,
            dto.DeletedTweetsPreserved,
            dto.CreatedFolderCount);

        return Result.Success(dto);
    }
}
