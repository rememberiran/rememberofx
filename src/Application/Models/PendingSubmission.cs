using Domain.Entities;

namespace Application.Models;

public record PendingSubmission(
    TweetWithAuthor Tweet,
    IReadOnlyList<PendingFolder> RequestedFolders);

public record PendingFolder(Guid FolderId, string FolderName, DateTime SubmittedAt);
