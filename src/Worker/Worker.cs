namespace Worker;

public class ScrapeWorker : BackgroundService
{
    private readonly ILogger<ScrapeWorker> _logger;

    public ScrapeWorker(ILogger<ScrapeWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scrape Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Waiting for scrape jobs...");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
