using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class XUserProfileService : IXUserProfileService
{
    private readonly IAppDbContext _db;
    private static readonly EventId ProfileCreatedEvent = new(1060, "XUserProfileCreated");
    private static readonly EventId ProfileUpdatedEvent = new(1061, "XUserProfileUpdated");
    private static readonly EventId AuthorStatsRetrievedEvent = new(1062, "AuthorStatsRetrieved");

    private readonly ILogger<XUserProfileService> _logger;

    public XUserProfileService(IAppDbContext db, ILogger<XUserProfileService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<XUserProfile>> GetByXUserIdAsync(string xUserId, CancellationToken ct)
    {
        var profile = await _db.XUserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.XUserId == xUserId || p.XUsername == xUserId, ct);
        if (profile is null)
        {
            return Result.Failure<XUserProfile>(DomainError.NotFound($"X user profile not found"));
        }

        return Result.Success(XUserProfileMapper.ToDomain(profile));
    }

    public async Task<Result<AuthorStats>> GetAuthorStatsAsync(string xUserId, CancellationToken ct)
    {
        var stats = await _db.Tweets.AsNoTracking()
            .Where(t => t.AuthorXUserId == xUserId && t.FetchStatus == "Ok")
            .GroupBy(_ => 1)
            .Select(g => new AuthorStats(
                g.Count(),
                g.Sum(t => t.VoteCount),
                g.Min(t => t.CreatedAt)))
            .FirstOrDefaultAsync(ct);

        _logger.LogInformation(AuthorStatsRetrievedEvent, "Author stats retrieved for {XUserId}", xUserId);

        return Result.Success(stats ?? new AuthorStats(0, 0, null));
    }

    public async Task<Result<XUserProfile>> UpsertAsync(string xUserId, string? customName, string? description, Guid? updatedByUserId, CancellationToken ct)
    {
        if (customName is null && description is null)
        {
            return Result.Failure<XUserProfile>(DomainError.Validation($"At least one field (customName or description) is required"));
        }

        var profile = await _db.XUserProfiles.FirstOrDefaultAsync(p => p.XUserId == xUserId, ct);

        if (profile is null)
        {
            profile = new XUserProfileRecord
            {
                Id = Guid.NewGuid(),
                XUserId = xUserId,
                CustomName = customName,
                Description = description,
                CreatedByUserId = updatedByUserId,
            };
            _db.XUserProfiles.Add(profile);
            _logger.LogInformation(ProfileCreatedEvent, "XUserProfile created for {XUserId}", xUserId);
        }
        else
        {
            if (customName != null)
            {
                profile.CustomName = customName;
            }

            if (description != null)
            {
                profile.Description = description;
            }

            profile.UpdatedByUserId = updatedByUserId;
            profile.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation(ProfileUpdatedEvent, "XUserProfile updated for {XUserId}", xUserId);
        }

        return Result.Success(XUserProfileMapper.ToDomain(profile));
    }
}
