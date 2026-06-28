namespace Application.Models;

public record FolderDto(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    Guid? ParentFolderId,
    int ChildCount,
    DateTime CreatedAt,
    bool IsActive,
    IReadOnlyList<FolderSummaryDto>? Children = null,
    IReadOnlyList<FolderBreadcrumbDto>? Breadcrumb = null);

public record FolderSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    int ChildCount,
    int TweetCount);

public record FolderBreadcrumbDto(
    Guid Id,
    string Name);
