using Application.Models;

namespace Application.Interfaces;

public interface IUserService
{
    Task<Result<List<UserDto>>> ListAllAsync(CancellationToken ct);
    Task<Result<UserDto>> GetByXUserIdAsync(string xUserId, CancellationToken ct);
    Task<Result<UserDto>> AddAsync(string xUserId, string role, Guid? createdByUserId, CancellationToken ct);
    Task<Result<UserDto>> UpdateAsync(Guid id, string? role, bool? isActive, CancellationToken ct);
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct);
}
