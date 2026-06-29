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
    string Visibility,
    string? OwnerUsername,
    int Depth,
    IReadOnlyList<FolderSummaryDto>? Children = null,
    IReadOnlyList<FolderBreadcrumbDto>? Breadcrumb = null);

public record FolderSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    int ChildCount,
    int TweetCount,
    string Visibility,
    string? OwnerUsername,
    DateTime CreatedAt);

public record FolderBreadcrumbDto(
    Guid Id,
    string Name);
