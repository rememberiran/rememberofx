using Application.Interfaces;
using Application.Models;
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

    public async Task<Result<XUserProfileDto>> GetByXUserIdAsync(string xUserId, CancellationToken ct)
    {
        var profile = await _db.XUserProfiles.FirstOrDefaultAsync(p => p.XUserId == xUserId, ct);
        if (profile is null)
            return Result.Failure<XUserProfileDto>(DomainError.NotFound("X user profile not found"));

        return Result.Success(MapToDto(profile));
    }

    public async Task<Result<XUserProfileDto>> UpsertAsync(string xUserId, string? customName, string? description, Guid? updatedByUserId, CancellationToken ct)
    {
        if (customName is null && description is null)
            return Result.Failure<XUserProfileDto>(DomainError.Validation("At least one field (customName or description) is required"));

        var profile = await _db.XUserProfiles.FirstOrDefaultAsync(p => p.XUserId == xUserId, ct);

        if (profile is null)
        {
            profile = new XUserProfileRecord
            {
                Id = Guid.NewGuid(),
                XUserId = xUserId,
                CustomName = customName,
                Description = description,
                CreatedByUserId = updatedByUserId
            };
            _db.XUserProfiles.Add(profile);
            _logger.LogInformation("XUserProfile created for {XUserId}", xUserId);
        }
        else
        {
            if (customName != null) profile.CustomName = customName;
            if (description != null) profile.Description = description;
            profile.UpdatedByUserId = updatedByUserId;
            profile.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("XUserProfile updated for {XUserId}", xUserId);
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(MapToDto(profile));
    }

    private static XUserProfileDto MapToDto(XUserProfileRecord record)
    {
        return new XUserProfileDto(record.Id, record.XUserId, record.ScrapedUsername, record.CustomName, record.Description);
    }
}
