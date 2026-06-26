namespace Application.Models;

public record FolderDto(
    Guid Id,
    string Name,
    string? Description,
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
    int ChildCount);

public record FolderBreadcrumbDto(
    Guid Id,
    string Name);
