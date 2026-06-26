namespace Application.Interfaces;

public record XApiUserInfo(string Id, string Username);

public interface IXApiClient
{
    Task<XApiUserInfo?> GetCurrentUserAsync(string accessToken, CancellationToken ct);
}
