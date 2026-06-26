namespace Application.Models;

public record XUserProfileDto(
    Guid Id,
    string XUserId,
    string? ScrapedUsername,
    string? CustomName,
    string? Description);
