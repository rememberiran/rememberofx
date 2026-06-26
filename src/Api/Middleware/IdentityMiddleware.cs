using System.Security.Claims;
using Application;
using Application.Models;

namespace Api.Middleware;

public class IdentityMiddleware
{
    private readonly RequestDelegate _next;

    public IdentityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAsyncContext<IdentityContext> identityContext)
    {
        var ipAddress = context.Request.Headers[$"X-Forwarded-For"].FirstOrDefault()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? $"unknown";

        var user = context.User;

        identityContext.Value = new IdentityContext
        {
            UserId = user.FindFirstValue($"sub"),
            Username = user.FindFirstValue($"username"),
            Email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue($"email"),
            IpAddress = ipAddress,
        };

        await _next(context);
    }
}
