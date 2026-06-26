namespace Application.Models;

public record FolderDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentFolderId,
    int ChildCount,
    DateTime CreatedAt,
    bool IsActive,
    List<FolderSummaryDto>? Children = null,
    List<FolderBreadcrumbDto>? Breadcrumb = null);

public record FolderSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    int ChildCount);

public record FolderBreadcrumbDto(
    Guid Id,
    string Name);
