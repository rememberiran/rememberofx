using Application.Interfaces;
using Domain.Entities;
using Domain.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storage;

namespace Application.Services;

public class UserService : IUserService
{
    private readonly IAppDbContext _db;
    private static readonly EventId UserAddedEvent = new(1010, "UserAdded");
    private static readonly EventId UserUpdatedEvent = new(1011, "UserUpdated");
    private static readonly EventId UserDeactivatedEvent = new(1012, "UserDeactivated");

    private readonly ILogger<UserService> _logger;

    public UserService(IAppDbContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<List<User>>> ListAllAsync(CancellationToken ct)
    {
        var records = await _db.Users.AsNoTracking().ToListAsync(ct);
        var users = records.Select(r => UserMapper.ToDomain(r)).ToList();
        return Result.Success(users);
    }

    public async Task<Result<User>> GetByXUserIdAsync(string xUserId, CancellationToken ct)
    {
        var user = await _db
            .Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.XUserId == xUserId && u.IsActive, ct);

        if (user is null)
        {
            return Result.Failure<User>(DomainError.NotFound("User not found"));
        }

        return Result.Success(UserMapper.ToDomain(user));
    }

    public async Task<Result<User>> AddAsync(string xUserId, string role, Guid? createdByUserId, CancellationToken ct)
    {
        var existing = await _db
            .Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.XUserId == xUserId, ct);

        if (existing != null)
        {
            return Result.Failure<User>(DomainError.Conflict("User with this X user ID already exists"));
        }

        var user = new UserRecord
        {
            Id = Guid.NewGuid(),
            XUserId = xUserId,
            Role = role,
            CreatedByUserId = createdByUserId,
        };

        _db.Users.Add(user);

        _logger.LogInformation(UserAddedEvent, "User added: {UserId}, XUserId: {XUserId}, Role: {Role}", user.Id, xUserId, role);

        return Result.Success(UserMapper.ToDomain(user));
    }

    public async Task<Result<User>> UpdateAsync(Guid id, string? role, bool? isActive, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Result.Failure<User>(DomainError.NotFound("User not found"));
        }

        if (role != null)
        {
            user.Role = role;
        }

        if (isActive.HasValue)
        {
            user.IsActive = isActive.Value;
        }

        _logger.LogInformation(UserUpdatedEvent, "User updated: {UserId}, Role: {Role}, IsActive: {IsActive}", id, user.Role, user.IsActive);

        return Result.Success(UserMapper.ToDomain(user));
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound("User not found"));
        }

        user.IsActive = false;

        _logger.LogInformation(UserDeactivatedEvent, "User deactivated: {UserId}", id);

        return Result.Success();
    }
}
