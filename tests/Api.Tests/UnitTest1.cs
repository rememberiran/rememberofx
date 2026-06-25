namespace Api.Tests;

public class HealthEndpointTests
{
    [Fact]
    public void HealthLive_ReturnsHealthy()
    {
        var controller = new Api.Controllers.HealthController();
        var result = controller.Live();
        Assert.NotNull(result);
    }

    [Fact]
    public void HealthReady_ReturnsReady()
    {
        var controller = new Api.Controllers.HealthController();
        var result = controller.Ready();
        Assert.NotNull(result);
    }
}
