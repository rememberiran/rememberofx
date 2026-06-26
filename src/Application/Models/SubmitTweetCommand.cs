namespace Application.Models;

public record SubmitTweetCommand(
    string TweetUrl,
    List<Guid>? FolderIds,
    string SubmittedByIp,
    Guid? SubmittedByUserId);
