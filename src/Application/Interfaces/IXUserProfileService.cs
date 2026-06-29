using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface IXUserProfileService
{
    Task<Result<XUserProfile>> GetByXUserIdAsync(string xUserId, CancellationToken ct);
    Task<Result<XUserProfile>> UpsertAsync(string xUserId, string? customName, string? description, Guid? updatedByUserId, CancellationToken ct);
    Task<Result<AuthorStats>> GetAuthorStatsAsync(string xUserId, CancellationToken ct);
}
