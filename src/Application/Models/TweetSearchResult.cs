using Domain.Entities;

namespace Application.Models;

public record TweetSearchResult(
    IReadOnlyList<TweetWithAuthor> Items,
    int TotalCount,
    XUserProfile? SubjectProfile = null);
