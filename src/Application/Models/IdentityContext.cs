namespace Application.Models;

public class IdentityContext
{
    public string? UserId { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string IpAddress { get; init; } = default!;
}
