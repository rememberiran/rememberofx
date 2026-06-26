using System.Security.Claims;
using Application;
using Application.Models;

namespace Api.Middleware;

public class IdentityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdentityMiddleware> _logger;

    public IdentityMiddleware(RequestDelegate next, ILogger<IdentityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAsyncContext<IdentityContext> identityContext)
    {
        var ipAddress = context.Request.Headers[$"X-Forwarded-For"].FirstOrDefault()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "<unknown>";

        var user = context.User;
        var username = user.FindFirstValue($"username");

        identityContext.Value = new IdentityContext
        {
            UserId = user.FindFirstValue($"sub"),
            Username = username,
            Email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue($"email"),
            IpAddress = ipAddress,
        };

        using (_logger.BeginScope(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Username"] = username,
            ["IpAddress"] = ipAddress,
        }))
        {
            await _next(context);
        }
    }
}
