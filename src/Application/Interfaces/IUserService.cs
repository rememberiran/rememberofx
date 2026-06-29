using Domain.Entities;

namespace Application.Interfaces;

public interface IUserService
{
    Task<Result<List<User>>> ListAllAsync(CancellationToken ct);
    Task<Result<User>> GetByXUserIdAsync(string xUserId, CancellationToken ct);
    Task<Result<User>> AddAsync(string xUserId, string role, Guid? createdByUserId, CancellationToken ct);
    Task<Result<User>> UpdateAsync(Guid id, string? role, bool? isActive, CancellationToken ct);
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct);
}
