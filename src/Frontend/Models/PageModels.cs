namespace Frontend.Models;

public record HomePageModel(
    IReadOnlyList<FolderViewModel> Folders,
    IReadOnlyList<TweetViewModel> TopVotedTweets);

public record BrowsePageModel(
    IReadOnlyList<FolderViewModel> Folders);

public record FolderDetailPageModel(
    FolderViewModel Folder,
    IReadOnlyList<BreadcrumbItemViewModel> Breadcrumb,
    IReadOnlyList<SubfolderViewModel> Subfolders,
    IReadOnlyList<TweetViewModel> Tweets,
    int TotalTweetCount,
    int CurrentPage,
    int TotalPages,
    string Sort,
    int Depth = 1,
    int MaxDepth = 5);

public record TweetDetailPageModel(
    TweetViewModel Tweet,
    IReadOnlyList<BreadcrumbItemViewModel> Breadcrumb,
    ProfileViewModel? AuthorProfile,
    CaptureInfoViewModel? Capture);

public record SearchPageModel(
    IReadOnlyList<TweetViewModel> Results,
    int TotalCount,
    int CurrentPage,
    int TotalPages,
    string? Query,
    string? Tag,
    string? Username,
    string Sort,
    ProfileViewModel? SubjectProfile,
    IReadOnlyList<FolderViewModel> FolderResults = null!);

public record SubmitPageModel(
    IReadOnlyList<FolderOptionViewModel> AvailableFolders);

public record AdminPageModel(
    IReadOnlyList<UserViewModel> Users);

public record ProfilePageModel(
    ProfileViewModel Profile,
    IReadOnlyList<TweetViewModel> Tweets,
    int TotalCount,
    int CurrentPage,
    int TotalPages,
    string Sort);

public record MyArchivePageModel(
    IReadOnlyList<TweetViewModel> SubmittedTweets,
    IReadOnlyList<MyFolderViewModel> CreatedFolders,
    int TotalVotesEarned,
    int DeletedTweetsPreserved);
