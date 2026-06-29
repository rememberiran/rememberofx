using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Api.Middleware;

public sealed class ThrottlingMiddleware : IDisposable
{
    private const int AnonymousLimitPerMinute = 30;
    private const int AuthenticatedLimitPerMinute = 100;

    private readonly RequestDelegate _next;
    private readonly PartitionedRateLimiter<HttpContext> _limiter;

    public ThrottlingMiddleware(RequestDelegate next)
    {
        _next = next;
        _limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var sub = context.User.FindFirstValue($"sub");

            if (sub != null)
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    string.Concat($"u:", sub),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = AuthenticatedLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                    });
            }

            var ip = context.Request.Headers[$"X-Client-Ip"].FirstOrDefault()
                     ?? context.Request.Headers[$"X-Forwarded-For"].FirstOrDefault()
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? $"unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                string.Concat($"ip:", ip),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AnonymousLimitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                });
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var lease = await _limiter.AcquireAsync(context, permitCount: 1, context.RequestAborted);

        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers[$"Retry-After"] = $"60";
            await context.Response.WriteAsJsonAsync(
                new { error = $"TooManyRequests", message = $"Rate limit exceeded. Please try again later." },
                context.RequestAborted);
            return;
        }

        await _next(context);
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }
}
