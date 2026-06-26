using Microsoft.AspNetCore.Diagnostics;

namespace Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString();

        _logger.LogError(exception,
            "Unhandled exception on {Method} {Path}. CorrelationId: {CorrelationId}",
            context.Request.Method, context.Request.Path, correlationId);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "INTERNAL_ERROR",
            message = "An unexpected error occurred.",
            correlationId
        }, ct);

        return true;
    }
}
