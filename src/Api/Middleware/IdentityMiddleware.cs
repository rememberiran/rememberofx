using System.Security.Claims;
using Application;
using Application.Models;
using Microsoft.EntityFrameworkCore;

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

    public async Task InvokeAsync(HttpContext context, IAsyncContext<IdentityContext> identityContext, IAppDbContext db)
    {
        var ipAddress = context.Request.Headers[$"X-Forwarded-For"].FirstOrDefault()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "<unknown>";

        var user = context.User;
        var xUserId = user.FindFirstValue($"sub");
        var username = user.FindFirstValue($"username");

        Guid? internalUserId = null;
        if (xUserId != null)
        {
            var dbUser = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.XUserId == xUserId && u.IsActive, context.RequestAborted);

            if (dbUser is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Forbidden", message = "Access denied — your account is not registered or has been deactivated" },
                    context.RequestAborted);
                return;
            }

            internalUserId = dbUser.Id;
        }

        identityContext.Value = new IdentityContext
        {
            XUserId = xUserId,
            InternalUserId = internalUserId,
            XUsername = username,
            XEmail = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue($"email"),
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
