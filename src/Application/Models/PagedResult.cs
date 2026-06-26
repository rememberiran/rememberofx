namespace Application.Models;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
