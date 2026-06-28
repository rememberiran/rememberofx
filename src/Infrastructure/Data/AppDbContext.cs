using Application;
using Microsoft.EntityFrameworkCore;
using Storage;

namespace Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<TweetRecord> Tweets => Set<TweetRecord>();
    public DbSet<XUserProfileRecord> XUserProfiles => Set<XUserProfileRecord>();
    public DbSet<FolderRecord> Folders => Set<FolderRecord>();
    public DbSet<FolderTweetRecord> FolderTweets => Set<FolderTweetRecord>();
    public DbSet<VoteRecord> Votes => Set<VoteRecord>();
    public DbSet<TweetMediaRecord> TweetMedia => Set<TweetMediaRecord>();
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();
    public DbSet<FolderClosureRecord> FolderClosures => Set<FolderClosureRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRecord>(e =>
        {
            e.ToTable($"Users");
            e.HasIndex(u => u.XUserId).IsUnique();
            e.Property(u => u.XUserId).HasMaxLength(50);
            e.Property(u => u.XUsername).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(20);
        });

        modelBuilder.Entity<TweetRecord>(e =>
        {
            e.ToTable($"Tweets");
            e.HasIndex(t => t.XTweetId).IsUnique();
            e.HasIndex(t => t.AuthorXUserId);
            e.HasIndex(t => t.VoteCount).IsDescending();
            e.HasIndex(t => t.CreatedAt).IsDescending();
            e.HasIndex(t => t.FetchStatus);
            e.Property(t => t.XTweetId).HasMaxLength(50);
            e.Property(t => t.XTweetUrl).HasMaxLength(500);
            e.Property(t => t.AuthorXUserId).HasMaxLength(50);
            e.Property(t => t.AuthorXUsername).HasMaxLength(100);
            e.Property(t => t.ScreenshotBlobName).HasMaxLength(200);
            e.Property(t => t.ScrapeError).HasMaxLength(1000);
            e.Property(t => t.SubmittedByIp).HasMaxLength(50);
            e.Property(t => t.FetchStatus).HasMaxLength(20);
        });

        modelBuilder.Entity<XUserProfileRecord>(e =>
        {
            e.ToTable($"XUserProfiles");
            e.HasIndex(x => x.XUserId).IsUnique();
            e.Property(x => x.XUserId).HasMaxLength(50);
            e.Property(x => x.XUsername).HasMaxLength(100);
            e.Property(x => x.CustomName).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<FolderRecord>(e =>
        {
            e.ToTable($"Folders");
            e.HasIndex(f => f.ParentFolderId);
            e.Property(f => f.Name).HasMaxLength(200);
            e.Property(f => f.Description).HasMaxLength(1000);
            e.Property(f => f.Icon).HasMaxLength(50);
            e.HasOne(f => f.ParentFolder)
             .WithMany(f => f.Children)
             .HasForeignKey(f => f.ParentFolderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderTweetRecord>(e =>
        {
            e.ToTable($"FolderTweets");
            e.HasKey(ft => new { ft.FolderId, ft.TweetId });
        });

        modelBuilder.Entity<FolderClosureRecord>(e =>
        {
            e.ToTable("FolderClosures");
            e.HasKey(fc => new { fc.AncestorId, fc.DescendantId });
            e.HasIndex(fc => fc.DescendantId);
            e.HasOne(fc => fc.Ancestor)
             .WithMany()
             .HasForeignKey(fc => fc.AncestorId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(fc => fc.Descendant)
             .WithMany()
             .HasForeignKey(fc => fc.DescendantId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TweetMediaRecord>(e =>
        {
            e.ToTable($"TweetMedia");
            e.HasIndex(m => m.TweetId);
            e.Property(m => m.MediaType).HasMaxLength(10);
            e.Property(m => m.BlobName).HasMaxLength(200);
            e.Property(m => m.OriginalUrl).HasMaxLength(500);
            e.HasOne(m => m.Tweet)
             .WithMany(t => t.Media)
             .HasForeignKey(m => m.TweetId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VoteRecord>(e =>
        {
            e.ToTable($"Votes");
            e.HasIndex(v => new { v.TweetId, v.VoterIp }).IsUnique();
            e.Property(v => v.VoterIp).HasMaxLength(50);
        });

        modelBuilder.Entity<AuditLogRecord>(e =>
        {
            e.ToTable($"AuditLog");
            e.HasIndex(a => a.CorrelationId);
            e.Property(a => a.CorrelationId).HasMaxLength(36);
            e.Property(a => a.Action).HasMaxLength(100);
            e.Property(a => a.EntityType).HasMaxLength(50);
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.Property(a => a.IpAddress).HasMaxLength(50);
            e.Property(a => a.Region).HasMaxLength(100);
        });
    }
}
