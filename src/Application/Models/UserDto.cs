namespace Application.Models;

public record UserDto(
    Guid Id,
    string XUserId,
    string XUsername,
    string? Role,
    bool IsActive,
    DateTime CreatedAt);
