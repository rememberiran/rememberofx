using Microsoft.AspNetCore.Diagnostics;

namespace Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly EventId UnhandledExceptionEvent = new(2010, "UnhandledException");

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString();

        _logger.LogError(
            UnhandledExceptionEvent,
            exception,
            "Unhandled exception on {Method} {Path}. CorrelationId: {CorrelationId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            correlationId);

        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "INTERNAL_ERROR",
                message = "An unexpected error occurred.",
                correlationId,
            },
            cancellationToken);

        return true;
    }
}
