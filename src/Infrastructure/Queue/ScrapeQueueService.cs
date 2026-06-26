using System.Text.Json;
using Application.Interfaces;
using Application.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Queue;

public class ScrapeQueueService : IScrapeQueueService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<ScrapeQueueService> _logger;

    public ScrapeQueueService(IConfiguration configuration, ILogger<ScrapeQueueService> logger)
    {
        var accountUrl = configuration["Queue:AccountUrl"];
        var queueName = configuration["Queue:QueueName"] ?? "scrape-jobs";

        _queueClient = new QueueClient(new Uri($"{accountUrl}{queueName}"), new Azure.Identity.DefaultAzureCredential());
        _logger = logger;
    }

    public async Task EnqueueAsync(ScrapeJobMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        await _queueClient.SendMessageAsync(base64, cancellationToken: ct);
        _logger.LogInformation("Enqueued scrape job for {XTweetId}, CorrelationId: {CorrelationId}", message.XTweetId, message.CorrelationId);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        var properties = await _queueClient.GetPropertiesAsync(ct);
        return properties.HasValue;
    }
}
