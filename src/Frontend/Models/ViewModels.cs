namespace Frontend.Models;

public record TweetViewModel(
    Guid Id,
    string Author,
    string? AuthorDisplayName,
    string Date,
    string Text,
    int VoteCount,
    bool IsDeleted,
    string? ScreenshotUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaItemViewModel> Media,
    string? TweetUrl,
    CaptureInfoViewModel? Capture,
    IReadOnlyList<FolderChipViewModel> Folders,
    string? SubmittedDate = null,
    string? FetchStatus = null);

public record MediaItemViewModel(string Type, string? Url);

public record CaptureInfoViewModel(string CapturedBy, string CapturedAt);

public record FolderChipViewModel(Guid Id, string Name);

public record FolderViewModel(
    Guid Id,
    string Name,
    string? Description,
    string Icon,
    int SubfolderCount,
    int TweetCount,
    string Visibility = "private",
    string? OwnerUsername = null,
    string? OwnerInitial = null,
    int Depth = 1,
    DateTime? CreatedAt = null);

public record SubfolderViewModel(
    Guid Id,
    string Name,
    int TweetCount,
    string Visibility = "private",
    int SubfolderCount = 0);

public record BreadcrumbItemViewModel(string Label, string? Href);

public record UserViewModel(
    Guid Id,
    string XUsername,
    string XUserId,
    string Role,
    string Status,
    string Created);

public record ProfileViewModel(
    string XUsername,
    string XUserId,
    string? DisplayName,
    string? Description,
    int ArchivedTweetCount,
    int TotalVotes,
    string? FirstArchivedDate);

public record FolderOptionViewModel(Guid Id, string Name, int TweetCount);

public record MyFolderViewModel(
    Guid Id,
    string Name,
    string? Description,
    int TweetCount,
    int SubfolderCount,
    string Created,
    string Visibility = "private",
    bool IsNew = false);

public record MoveTargetViewModel(Guid Id, string Label);
