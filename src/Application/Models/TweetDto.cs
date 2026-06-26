namespace Application.Models;

public record TweetDto(
    Guid Id,
    string XTweetId,
    string XTweetUrl,
    string? AuthorXUserId,
    string? AuthorXUsername,
    string? TweetText,
    DateTime? TweetDate,
    string? ScreenshotUrl,
    string? Tags,
    int VoteCount,
    string FetchStatus,
    DateTime CreatedAt,
    XUserProfileDto? AuthorProfile = null);

public record TweetStatusDto(
    Guid TweetId,
    string FetchStatus,
    object? TweetData);
