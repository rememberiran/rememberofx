using Application;
using Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public class HealthEndpointTests
{
    [Fact]
    public void HealthLive_ReturnsHealthy()
    {
        var controller = new Api.Controllers.HealthController(
            new FakeDbContext(),
            new FakeQueueService(),
            NullLogger<Api.Controllers.HealthController>.Instance);
        var result = controller.Live();
        Assert.NotNull(result);
    }

    private sealed class FakeDbContext : IAppDbContext
    {
        public Microsoft.EntityFrameworkCore.DbSet<Storage.UserRecord> Users => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.DbSet<Storage.TweetRecord> Tweets => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.DbSet<Storage.XUserProfileRecord> XUserProfiles => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.DbSet<Storage.FolderRecord> Folders => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.DbSet<Storage.FolderTweetRecord> FolderTweets => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.DbSet<Storage.VoteRecord> Votes => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.DbSet<Storage.AuditLogRecord> AuditLogs => throw new NotSupportedException();
        public Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeQueueService : IScrapeQueueService
    {
        public Task EnqueueAsync(Application.Models.ScrapeJobMessage message, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> IsHealthyAsync(CancellationToken ct) => Task.FromResult(true);
    }
}
