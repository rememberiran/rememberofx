using Application.Models;

namespace Api.Models.Responses;

public record SubmitTweetResponse(Guid TweetId, string FetchStatus, SubmissionQuotaDto Quota);

public record SubmissionQuotaDto(
    int HourlyRemaining,
    int HourlyLimit,
    DateTime HourlyResetAt,
    int DailyRemaining,
    int DailyLimit,
    DateTime DailyResetAt);

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

public record TrustedContributorDto(string TrustedXUsername, DateTime CreatedAt);

public record PendingSubmissionDto(
    TweetDto Tweet,
    IReadOnlyList<PendingFolderDto> RequestedFolders);

public record PendingFolderDto(Guid FolderId, string FolderName, DateTime SubmittedAt);

public record ContributionStatsResponse(int AddedByOwner, int ContributedByCommunity);
