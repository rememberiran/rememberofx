using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAppDbContext db, IXApiClient xApiClient, IOptions<JwtSettings> jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _xApiClient = xApiClient;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResult>> ExchangeTokenAsync(string xAccessToken, string ipAddress, CancellationToken ct)
    {
        var xUser = await _xApiClient.GetCurrentUserAsync(xAccessToken, ct);
        if (xUser is null)
        {
            return Result.Failure<AuthTokenResult>(DomainError.Unauthorized($"Invalid or expired X token"));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.XUserId == xUser.Id, ct);

        if (user is null)
        {
            WriteAuditLog($"Auth.Denied", xUser.Id, ipAddress);
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Auth denied for unregistered X user {XUserId}", xUser.Id);
            return Result.Failure<AuthTokenResult>(DomainError.Forbidden($"Access denied — your X account is not registered"));
        }

        if (!user.IsActive)
        {
            WriteAuditLog($"Auth.Denied", user.XUserId, ipAddress);
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Auth denied for deactivated user {XUserId}", user.XUserId);
            return Result.Failure<AuthTokenResult>(DomainError.Forbidden($"User account is deactivated"));
        }

        if (!string.Equals(user.XUsername, xUser.Username, StringComparison.Ordinal))
        {
            user.XUsername = xUser.Username;
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(_jwt.ExpiryHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.XUserId),
            new Claim($"username", xUser.Username),
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

        WriteAuditLog($"Auth.Login", user.XUserId, ipAddress);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("JWT generated for user {XUserId}, role {Role}", user.XUserId, user.Role);

        return Result.Success(new AuthTokenResult(tokenString, expiresAt));
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
