namespace Application.Models;

public class IdentityContext
{
    public string? XUserId { get; init; }
    public Guid? InternalUserId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public string? XUsername { get; init; }
    public string? XEmail { get; init; }
    public string IpAddress { get; init; } = default!;
}
