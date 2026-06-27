namespace Frontend.Services;

public class LoggingDelegatingHandler : DelegatingHandler
{
    private static readonly EventId OutboundRequestEvent = new(6001, "OutboundRequest");
    private static readonly EventId OutboundResponseEvent = new(6002, "OutboundResponse");

    private readonly ILogger<LoggingDelegatingHandler> _logger;

    public LoggingDelegatingHandler(ILogger<LoggingDelegatingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            OutboundRequestEvent,
            "Outbound {Method} {Uri}",
            request.Method,
            request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken);

        _logger.LogInformation(
            OutboundResponseEvent,
            "Outbound response {StatusCode} from {Uri}",
            (int)response.StatusCode,
            request.RequestUri);

        return response;
    }
}
