using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Tweet> Tweets => Set<Tweet>();
    public DbSet<XUserProfile> XUserProfiles => Set<XUserProfile>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<FolderTweet> FolderTweets => Set<FolderTweet>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.XUserId).IsUnique();
            e.Property(u => u.XUserId).HasMaxLength(50);
            e.Property(u => u.XUsername).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(20);
        });

        modelBuilder.Entity<Tweet>(e =>
        {
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
            e.Property(t => t.FetchStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<XUserProfile>(e =>
        {
            e.HasIndex(x => x.XUserId).IsUnique();
            e.Property(x => x.XUserId).HasMaxLength(50);
            e.Property(x => x.ScrapedUsername).HasMaxLength(100);
            e.Property(x => x.CustomName).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<Folder>(e =>
        {
            e.HasIndex(f => f.ParentFolderId);
            e.Property(f => f.Name).HasMaxLength(200);
            e.Property(f => f.Description).HasMaxLength(1000);
            e.HasOne(f => f.ParentFolder)
             .WithMany(f => f.Children)
             .HasForeignKey(f => f.ParentFolderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderTweet>(e =>
        {
            e.HasKey(ft => new { ft.FolderId, ft.TweetId });
        });

        modelBuilder.Entity<Vote>(e =>
        {
            e.HasIndex(v => new { v.TweetId, v.VoterIp }).IsUnique();
            e.Property(v => v.VoterIp).HasMaxLength(50);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
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
