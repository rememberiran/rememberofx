namespace Application.Models;

public record SubmitTweetCommand(
    string TweetUrl,
    IReadOnlyList<Guid>? FolderIds,
    Guid? SubmittedByUserId);
