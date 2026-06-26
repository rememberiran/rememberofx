using Application;
using Application.Models;

namespace Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAsyncContext<CorrelationContext> correlationContext)
    {
        var correlationId = context.Request.Headers[$"X-Correlation-ID"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        correlationContext.Value = new CorrelationContext { CorrelationId = correlationId };

        context.Items[$"CorrelationId"] = correlationId;
        context.Response.Headers[$"X-Correlation-ID"] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { [$"CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
