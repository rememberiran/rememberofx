using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Storage;

namespace Application;

public interface IAppDbContext
{
    DbSet<UserRecord> Users { get; }
    DbSet<TweetRecord> Tweets { get; }
    DbSet<XUserProfileRecord> XUserProfiles { get; }
    DbSet<FolderRecord> Folders { get; }
    DbSet<FolderTweetRecord> FolderTweets { get; }
    DbSet<VoteRecord> Votes { get; }
    DbSet<TweetMediaRecord> TweetMedia { get; }
    DbSet<AuditLogRecord> AuditLogs { get; }
    DbSet<FolderClosureRecord> FolderClosures { get; }
    DbSet<TrustedContributorRecord> TrustedContributors { get; }
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
