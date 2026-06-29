using Application.Models;

namespace Api.Models.Responses;

public record SubmitTweetResponse(Guid TweetId, string FetchStatus);

public record TweetStatusResponse(Guid TweetId, string FetchStatus, object? TweetData);

public record SearchTweetsResponse(
    IReadOnlyList<TweetDto> Items,
    int TotalCount,
    XUserProfileDto? SubjectProfile);

public record FolderTweetsResponse(IReadOnlyList<TweetDto> Items, int TotalCount);

public record AuthTokenResponse(string Token, DateTime ExpiresAt);

public record VoteResponse(string Message);

public record HealthResponse(string Status);

public record ReadinessResponse(string Status, bool Db, bool Queue);

public record ErrorResponse(string Error, string Message);
