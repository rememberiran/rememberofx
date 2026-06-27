using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application;
using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Storage;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly IXApiClient _xApiClient;
    private readonly JwtSettings _jwt;
    private static readonly EventId AuthDeniedEvent = new(1001, "AuthDenied");
    private static readonly EventId DevTokenGeneratedEvent = new(1002, "DevTokenGenerated");
    private static readonly EventId JwtGeneratedEvent = new(1003, "JwtGenerated");

    private readonly IAsyncContext<IdentityContext> _identityContext;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppDbContext db,
        IXApiClient xApiClient,
        IOptions<JwtSettings> jwt,
        IAsyncContext<IdentityContext> identityContext,
        ILogger<AuthService> logger)
    {
        _db = db;
        _xApiClient = xApiClient;
        _jwt = jwt.Value;
        _identityContext = identityContext;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResult>> ExchangeTokenAsync(string xAccessToken, CancellationToken ct)
    {
        var ipAddress = _identityContext.Value!.IpAddress;
        var xUser = await _xApiClient.GetCurrentUserAsync(xAccessToken, ct);
        if (xUser is null)
        {
            return Result.Failure<AuthTokenResult>(DomainError.Unauthorized($"Invalid or expired X token"));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.XUserId == xUser.Id, ct);

        if (user is null)
        {
            WriteAuditLog($"Auth.Denied", xUser.Id, ipAddress);
            _logger.LogWarning(AuthDeniedEvent, "Auth denied for unregistered X user {XUserId}", xUser.Id);
            return Result.Failure<AuthTokenResult>(DomainError.Forbidden($"Access denied — your X account is not registered"));
        }

        if (!user.IsActive)
        {
            WriteAuditLog($"Auth.Denied", user.XUserId, ipAddress);
            _logger.LogWarning(AuthDeniedEvent, "Auth denied for deactivated user {XUserId}", user.XUserId);
            return Result.Failure<AuthTokenResult>(DomainError.Forbidden($"User account is deactivated"));
        }

        if (!string.Equals(user.XUsername, xUser.Username, StringComparison.Ordinal))
        {
            user.XUsername = xUser.Username;
        }

        WriteAuditLog($"Auth.Login", user.XUserId, ipAddress);

        return Result.Success(GenerateJwt(user));
    }

    public async Task<Result<AuthTokenResult>> GenerateDevTokenAsync(string xUserId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.XUserId == xUserId && u.IsActive, ct);
        if (user is null)
        {
            return Result.Failure<AuthTokenResult>(DomainError.NotFound($"User not found or inactive"));
        }

        _logger.LogInformation(DevTokenGeneratedEvent, "Dev token generated for user {XUserId}, role {Role}", user.XUserId, user.Role);

        return Result.Success(GenerateJwt(user));
    }

    private AuthTokenResult GenerateJwt(UserRecord user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(_jwt.ExpiryHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.XUserId),
            new Claim($"username", user.XUsername),
            new Claim($"role", user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation(JwtGeneratedEvent, "JWT generated for user {XUserId}, role {Role}", user.XUserId, user.Role);

        return new AuthTokenResult(tokenString, expiresAt);
    }

    private void WriteAuditLog(string action, string xUserId, string ipAddress)
    {
        _db.AuditLogs.Add(new AuditLogRecord
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Action = action,
            EntityType = $"User",
            EntityId = xUserId,
            IpAddress = ipAddress,
        });
    }
}
