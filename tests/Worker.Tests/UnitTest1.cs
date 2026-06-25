using Microsoft.Extensions.Logging.Abstractions;

namespace Worker.Tests;

public class ScrapeWorkerTests
{
    [Fact]
    public void ScrapeWorker_CanBeConstructed()
    {
        var logger = NullLogger<ScrapeWorker>.Instance;
        var worker = new ScrapeWorker(logger);
        Assert.NotNull(worker);
    }
}
