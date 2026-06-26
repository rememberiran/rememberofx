using Api.Models.Responses;
using Application;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IScrapeQueueService _queue;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IAppDbContext db, IScrapeQueueService queue, ILogger<HealthController> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    [HttpGet("live")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new HealthResponse($"Healthy"));
    }

    [HttpGet("ready")]
    [ProducesResponseType(typeof(ReadinessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReadinessResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var dbHealthy = false;
        var queueHealthy = false;

        try
        {
            dbHealthy = await _db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Database health check failed");
        }

        try
        {
            queueHealthy = await _queue.IsHealthyAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Queue health check failed");
        }

        var ready = dbHealthy && queueHealthy;
        var response = new ReadinessResponse(ready ? $"Ready" : $"Unhealthy", dbHealthy, queueHealthy);

        return ready ? Ok(response) : StatusCode(503, response);
    }
}
