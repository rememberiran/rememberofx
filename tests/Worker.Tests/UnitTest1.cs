using Microsoft.Extensions.Logging.Abstractions;

namespace Worker.Tests;

public class ScrapeWorkerTests
{
    [Fact]
    public void ScrapeWorkerCanBeConstructed()
    {
        var logger = NullLogger<ScrapeWorker>.Instance;
        using var worker = new ScrapeWorker(logger);
        Assert.NotNull(worker);
    }
}
