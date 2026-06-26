using Domain.Entities;

namespace Application.Models;

public record FolderSummary(Folder Folder, int ActiveChildCount);
