using System.Diagnostics;

namespace Api.Middleware;

public class HttpLoggingMiddleware
{
    private static readonly EventId HttpRequestEvent = new(2001, "HttpRequest");
    private static readonly EventId HttpResponseEvent = new(2002, "HttpResponse");

    private readonly RequestDelegate _next;
    private readonly ILogger<HttpLoggingMiddleware> _logger;

    public HttpLoggingMiddleware(RequestDelegate next, ILogger<HttpLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

        _logger.LogInformation(
            HttpRequestEvent,
            "HTTP Request {Method} {Path}{QueryString} from {IpAddress}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            ipAddress);

        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        _logger.LogInformation(
            HttpResponseEvent,
            "HTTP Response {Method} {Path} -> {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.Elapsed.TotalMilliseconds);
    }
}
