namespace Application.Models;

public record SearchTweetsQuery(
    string? Q,
    string? Tag,
    string? Username,
    string? UserId,
    string Sort = "votes",
    int Page = 1,
    int PageSize = 20);

public record SearchTweetsResult(
    List<TweetDto> Items,
    int TotalCount,
    XUserProfileDto? SubjectProfile = null);
