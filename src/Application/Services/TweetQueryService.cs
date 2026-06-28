using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class TweetQueryService : ITweetQueryService
{
    private readonly IAppDbContext _db;

    public TweetQueryService(IAppDbContext db)
    {
        _db = db;
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
}
