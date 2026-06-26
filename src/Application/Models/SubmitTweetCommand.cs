namespace Application.Models;

public record SubmitTweetCommand(
    string TweetUrl,
    IReadOnlyList<Guid>? FolderIds,
    string SubmittedByIp,
    Guid? SubmittedByUserId);
