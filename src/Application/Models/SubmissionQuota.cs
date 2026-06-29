namespace Application.Models;

public record SubmissionQuota(
    int HourlyRemaining,
    int HourlyLimit,
    DateTime HourlyResetAt,
    int DailyRemaining,
    int DailyLimit,
    DateTime DailyResetAt);

public record SubmissionResultWithQuota(Domain.Entities.Tweet Tweet, SubmissionQuota Quota);
