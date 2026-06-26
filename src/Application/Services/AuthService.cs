using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAppDbContext db, IOptions<JwtSettings> jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<Result<string>> VerifyAndGenerateTokenAsync(string xUserId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.XUserId == xUserId, ct);

        if (user is null)
            return Result.Failure<string>(DomainError.NotFound("User not found"));

        if (!user.IsActive)
            return Result.Failure<string>(DomainError.Unauthorized("User account is deactivated"));

        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.XUserId),
            new Claim("username", user.XUsername),
            new Claim("role", user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            claims: claims,
            expires: now.AddHours(_jwt.ExpiryHours),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("JWT generated for user {XUserId}, role {Role}", xUserId, user.Role);

        return Result.Success(tokenString);
    }
}
