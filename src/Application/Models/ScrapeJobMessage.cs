namespace Application.Models;

public record ScrapeJobMessage(
    string XTweetUrl,
    string XTweetId,
    string AuthorXUsername,
    List<Guid>? FolderIds,
    Guid? SubmittedByUserId,
    string SubmittedByIp,
    string CorrelationId);
