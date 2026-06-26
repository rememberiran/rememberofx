using System.Net.Http.Headers;
using System.Text.Json;
using Application.Interfaces;

namespace Infrastructure.XApi;

public class XApiClient : IXApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public XApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<XApiUserInfo?> GetCurrentUserAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitter.com/2/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue($"Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<XApiMeResponse>(content, JsonOptions);

        return result?.Data is not null
            ? new XApiUserInfo(result.Data.Id, result.Data.Username)
            : null;
    }

#pragma warning disable CA1812 // Instantiated by JsonSerializer.Deserialize
    private sealed record XApiMeResponse(XApiMeData? Data);
    private sealed record XApiMeData(string Id, string Username);
#pragma warning restore CA1812
}
