using System.Security.Claims;

namespace Api.Extensions;

public static class HttpContextExtensions
{
    public static string GetClientIp(this HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? $"unknown";
    }

    public static string? GetXUserId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue($"sub");
    }

    public static string? GetUserRole(this ClaimsPrincipal user)
    {
        return user.FindFirstValue($"role") ?? user.FindFirstValue(ClaimTypes.Role);
    }
}
