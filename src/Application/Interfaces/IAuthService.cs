using Application.Models;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthTokenResult>> ExchangeTokenAsync(string xAccessToken, CancellationToken ct);
    Task<Result<AuthTokenResult>> GenerateDevTokenAsync(string xUserId, CancellationToken ct);
}
