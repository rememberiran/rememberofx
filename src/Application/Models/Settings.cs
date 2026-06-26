namespace Application.Models;

public class FolderSettings
{
    public int MaxPerContributor { get; set; } = 50;
    public int MaxTweetsPerFolder { get; set; } = 1000;
    public int MaxDepth { get; set; } = 5;
}

public class JwtSettings
{
    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public int ExpiryHours { get; set; } = 8;
}
