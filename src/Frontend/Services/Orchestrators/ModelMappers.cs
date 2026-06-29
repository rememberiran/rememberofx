using System.Globalization;
using Application.Models;
using Frontend.Models;

namespace Frontend.Services.Orchestrators;

internal static class ModelMappers
{
    private static readonly Dictionary<string, string> FolderIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Politics"] = "bi-bank",
        ["Tech & AI"] = "bi-cpu",
        ["Sports"] = "bi-trophy",
        ["Science"] = "bi-prism",
        ["Culture & Media"] = "bi-palette",
        ["Breaking News"] = "bi-broadcast",
    };

    public static string GetFolderIcon(string name) =>
        FolderIcons.TryGetValue(name, out var icon) ? icon : "bi-folder-fill";

    public static FolderViewModel ToFolderViewModel(FolderSummaryDto dto) =>
        new(
            dto.Id,
            dto.Name,
            dto.Description,
            dto.Icon ?? GetFolderIcon(dto.Name),
            dto.ChildCount,
            dto.TweetCount,
            dto.Visibility,
            dto.OwnerUsername,
            string.IsNullOrEmpty(dto.OwnerUsername) ? null : dto.OwnerUsername[..1].ToUpperInvariant(),
            CreatedAt: dto.CreatedAt);

    public static TweetViewModel ToTweetViewModel(TweetDto dto) =>
        new(
            dto.Id,
            dto.AuthorXUsername ?? "unknown",
            dto.AuthorProfile?.CustomName,
            dto.TweetDate?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) ?? dto.CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
            dto.TweetText ?? "",
            dto.VoteCount,
            string.Equals(dto.FetchStatus, "NotFound", StringComparison.Ordinal),
            dto.ScreenshotUrl,
            (dto.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            dto.Media.Select(m => new MediaItemViewModel(m.MediaType, m.Url)).ToList(),
            dto.XTweetUrl,
            null,
            new List<FolderChipViewModel>(),
            null,
            dto.FetchStatus);

    public static UserViewModel ToUserViewModel(UserDto dto) =>
        new(
            dto.Id,
            dto.XUsername,
            dto.XUserId,
            dto.Role,
            dto.IsActive ? "Active" : "Inactive",
            dto.CreatedAt.ToString("MMM yyyy", CultureInfo.InvariantCulture));

    public static ProfileViewModel ToProfileViewModel(
        XUserProfileDto dto,
        int tweetCount = 0,
        int totalVotes = 0) =>
        new(
            dto.ScrapedUsername ?? dto.XUserId,
            dto.XUserId,
            dto.CustomName,
            dto.Description,
            tweetCount,
            totalVotes,
            null);

    public static MyFolderViewModel ToMyFolderViewModel(FolderSummaryDto dto) =>
        new(
            dto.Id,
            dto.Name,
            dto.Description,
            dto.TweetCount,
            dto.ChildCount,
            dto.CreatedAt != default ? dto.CreatedAt.ToString("MMM yyyy", CultureInfo.InvariantCulture) : "",
            dto.Visibility,
            dto.CreatedAt > DateTime.UtcNow.AddDays(-1));
}
