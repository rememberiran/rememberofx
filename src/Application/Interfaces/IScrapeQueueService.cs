using Application.Models;

namespace Application.Interfaces;

public interface IScrapeQueueService
{
    Task EnqueueAsync(ScrapeJobMessage message, CancellationToken ct);
    Task<bool> IsHealthyAsync(CancellationToken ct);
}
