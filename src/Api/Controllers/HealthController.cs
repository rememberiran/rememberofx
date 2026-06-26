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
    public IActionResult Live()
    {
        return Ok(new { status = "Healthy" });
    }

    [HttpGet("ready")]
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
            _logger.LogWarning(ex, "Database health check failed");
        }

        try
        {
            queueHealthy = await _queue.IsHealthyAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Queue health check failed");
        }

        var ready = dbHealthy && queueHealthy;
        var response = new { status = ready ? "Ready" : "Unhealthy", db = dbHealthy, queue = queueHealthy };

        return ready ? Ok(response) : StatusCode(503, response);
    }
}
