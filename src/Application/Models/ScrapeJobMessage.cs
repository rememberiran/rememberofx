namespace Application.Models;

public record ScrapeJobMessage(
    string XTweetUrl,
    string XTweetId,
    string AuthorXUsername,
    IReadOnlyList<Guid>? FolderIds,
    Guid? SubmittedByUserId,
    string SubmittedByIp,
    string CorrelationId);
