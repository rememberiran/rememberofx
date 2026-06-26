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
    IReadOnlyList<TweetMediaDto> Media,
    XUserProfileDto? AuthorProfile = null);

public record TweetMediaDto(
    Guid Id,
    string MediaType,
    string? Url,
    int OrderIndex);

public record TweetStatusDto(
    Guid TweetId,
    string FetchStatus,
    object? TweetData);
