using Application.Models;
using Domain.Entities;

namespace Api.Mappers;

public static class FolderDtoMapper
{
    public static FolderDto ToDto(
        Folder folder,
        int childCount,
        IReadOnlyList<FolderSummaryDto>? children = null,
        IReadOnlyList<FolderBreadcrumbDto>? breadcrumb = null)
    {
        return new FolderDto(
            folder.Id,
            folder.Name,
            folder.Description,
            folder.Icon,
            folder.ParentFolderId,
            childCount,
            folder.CreatedAt,
            folder.IsActive,
            children,
            breadcrumb);
    }

    public static FolderSummaryDto ToSummaryDto(FolderSummary summary)
    {
        return new FolderSummaryDto(
            summary.Folder.Id,
            summary.Folder.Name,
            summary.Folder.Description,
            summary.Folder.Icon,
            summary.ActiveChildCount,
            summary.TweetCount);
    }

    public static FolderSummaryDto ToSummaryDto(Folder folder, int activeChildCount, int tweetCount)
    {
        return new FolderSummaryDto(
            folder.Id,
            folder.Name,
            folder.Description,
            folder.Icon,
            activeChildCount,
            tweetCount);
    }

    public static FolderBreadcrumbDto ToBreadcrumbDto(Folder folder)
    {
        return new FolderBreadcrumbDto(folder.Id, folder.Name);
    }
}
