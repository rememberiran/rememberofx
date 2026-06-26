using Application.Models;

namespace Application.Interfaces;

public interface IXUserProfileService
{
    Task<Result<XUserProfileDto>> GetByXUserIdAsync(string xUserId, CancellationToken ct);
    Task<Result<XUserProfileDto>> UpsertAsync(string xUserId, string? customName, string? description, Guid? updatedByUserId, CancellationToken ct);
}
