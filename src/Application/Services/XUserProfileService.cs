using Application.Interfaces;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class XUserProfileService : IXUserProfileService
{
    private readonly IAppDbContext _db;
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
            _logger.LogInformation("XUserProfile created for {XUserId}", xUserId);
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
            _logger.LogInformation("XUserProfile updated for {XUserId}", xUserId);
        }

        return Result.Success(XUserProfileMapper.ToDomain(profile));
    }
}
