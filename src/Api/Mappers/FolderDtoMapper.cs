using Application.Models;
using Domain.Entities;

namespace Api.Mappers;

public static class FolderDtoMapper
{
    public static FolderDto ToDto(
        Folder folder,
        int childCount,
        int depth = 1,
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
            folder.Visibility,
            folder.CreatedByUser?.XUsername,
            depth,
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
            summary.TweetCount,
            summary.Folder.Visibility,
            summary.Folder.CreatedByUser?.XUsername,
            summary.Folder.CreatedAt);
    }

    public static FolderBreadcrumbDto ToBreadcrumbDto(Folder folder)
    {
        return new FolderBreadcrumbDto(folder.Id, folder.Name);
    }
}
