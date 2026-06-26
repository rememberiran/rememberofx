using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class UserService : IUserService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<UserService> _logger;

    public UserService(IAppDbContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<List<UserDto>>> ListAllAsync(CancellationToken ct)
    {
        var users = await _db.Users
            .Select(u => new UserDto(u.Id, u.XUserId, u.XUsername, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(users);
    }

    public async Task<Result<UserDto>> GetByXUserIdAsync(string xUserId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.XUserId == xUserId && u.IsActive, ct);
        if (user is null)
            return Result.Failure<UserDto>(DomainError.NotFound("User not found"));

        return Result.Success(MapToDto(user));
    }

    public async Task<Result<UserDto>> AddAsync(string xUserId, string xUsername, string role, Guid? createdByUserId, CancellationToken ct)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.XUserId == xUserId, ct);
        if (existing != null)
            return Result.Failure<UserDto>(DomainError.Conflict("User with this X user ID already exists"));

        var user = new UserRecord
        {
            Id = Guid.NewGuid(),
            XUserId = xUserId,
            XUsername = xUsername,
            Role = role,
            CreatedByUserId = createdByUserId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User added: {UserId}, XUserId: {XUserId}, Role: {Role}", user.Id, xUserId, role);

        return Result.Success(MapToDto(user));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, string? role, bool? isActive, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Result.Failure<UserDto>(DomainError.NotFound("User not found"));

        if (role != null) user.Role = role;
        if (isActive.HasValue) user.IsActive = isActive.Value;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User updated: {UserId}, Role: {Role}, IsActive: {IsActive}", id, user.Role, user.IsActive);

        return Result.Success(MapToDto(user));
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Result.Failure(DomainError.NotFound("User not found"));

        user.IsActive = false;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User deactivated: {UserId}", id);

        return Result.Success();
    }

    private static UserDto MapToDto(UserRecord record)
    {
        return new UserDto(record.Id, record.XUserId, record.XUsername, record.Role, record.IsActive, record.CreatedAt);
    }
}
