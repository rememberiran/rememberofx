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

public record PendingSubmissionDto(
    TweetDto Tweet,
    IReadOnlyList<PendingFolderDto> RequestedFolders);

public record PendingFolderDto(Guid FolderId, string FolderName, DateTime SubmittedAt);

public record ContributionStatsResponse(int AddedByOwner, int ContributedByCommunity);

public record RemovalRequestDto(
    Guid Id,
    Guid FolderId,
    string FolderName,
    Guid TweetId,
    string TweetXId,
    Guid? RequestedByUserId,
    DateTime RequestedAt,
    string Status,
    DateTime? ResolvedAt,
    IReadOnlyList<RemovalApprovalDto> Approvals);

public record RemovalApprovalDto(
    Guid Id,
    Guid ApprovedByUserId,
    string ApprovedByXUsername,
    DateTime ApprovedAt,
    bool IsVoid);

public record ViolationReportDto(
    Guid Id,
    Guid ReportedUserId,
    string ReportedXUsername,
    Guid? ReportedByUserId,
    string Explanation,
    DateTime CreatedAt,
    string Status,
    DateTime? ReviewedAt);

public record ContributorProfileDto(
    Guid Id,
    string XUserId,
    string XUsername,
    string? Role,
    bool IsSuspended,
    string? SuspendedReason,
    int FolderCount,
    int TweetsAdded);

public record ContributionEntryDto(
    Guid? PerformedByUserId,
    string? PerformedByXUsername,
    string Action,
    string EntityType,
    string? EntityId,
    DateTime CreatedAt);

public record ContributionsResponse(
    IReadOnlyList<ContributionEntryDto> Items,
    int TotalCount);
