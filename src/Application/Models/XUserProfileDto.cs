namespace Application.Models;

public record XUserProfileDto(
    Guid Id,
    string XUserId,
    string? ScrapedUsername,
    string? CustomName,
    string? Description,
    int ArchivedTweetCount = 0,
    int TotalVotesReceived = 0,
    DateTime? FirstArchivedAt = null);
