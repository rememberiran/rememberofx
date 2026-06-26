namespace Application.Interfaces;

public interface IAuthService
{
    Task<Result<string>> VerifyAndGenerateTokenAsync(string xUserId, CancellationToken ct);
}
