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
    public DbSet<FolderTweetRemovalRequestRecord> RemovalRequests => Set<FolderTweetRemovalRequestRecord>();
    public DbSet<FolderTweetRemovalApprovalRecord> RemovalApprovals => Set<FolderTweetRemovalApprovalRecord>();
    public DbSet<ViolationReportRecord> ViolationReports => Set<ViolationReportRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRecord>(e =>
        {
            e.ToTable($"Users");
            e.HasIndex(u => u.XUserId).IsUnique();
            e.Property(u => u.XUserId).HasMaxLength(50);
            e.Property(u => u.XUsername).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(20);
            e.Property(u => u.SuspendedReason).HasMaxLength(500);
            e.HasOne(u => u.SuspendedByUser)
             .WithMany()
             .HasForeignKey(u => u.SuspendedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
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
            e.Property(t => t.IsAnonymous).HasDefaultValue(false);
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
            e.Property(f => f.Visibility).HasMaxLength(10).HasDefaultValue("private");
            e.HasOne(f => f.ParentFolder)
             .WithMany(f => f.Children)
             .HasForeignKey(f => f.ParentFolderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderTweetRecord>(e =>
        {
            e.ToTable($"FolderTweets");
            e.HasKey(ft => new { ft.FolderId, ft.TweetId });
            e.Property(ft => ft.Status).HasMaxLength(10).HasDefaultValue("approved");
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
            e.HasIndex(a => a.PerformedByUserId);
            e.HasIndex(a => a.Action);
            e.Property(a => a.CorrelationId).HasMaxLength(36);
            e.Property(a => a.Action).HasMaxLength(100);
            e.Property(a => a.EntityType).HasMaxLength(50);
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.Property(a => a.IpAddress).HasMaxLength(50);
            e.Property(a => a.Region).HasMaxLength(100);
        });

        modelBuilder.Entity<FolderTweetRemovalRequestRecord>(e =>
        {
            e.ToTable("RemovalRequests");
            e.HasIndex(r => r.Status);
            e.HasIndex(r => new { r.FolderId, r.TweetId });
            e.Property(r => r.Status).HasMaxLength(10).HasDefaultValue("pending");
            e.Property(r => r.RequestedByIp).HasMaxLength(50);
            e.HasOne(r => r.Folder)
             .WithMany()
             .HasForeignKey(r => r.FolderId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Tweet)
             .WithMany()
             .HasForeignKey(r => r.TweetId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.RequestedByUser)
             .WithMany()
             .HasForeignKey(r => r.RequestedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderTweetRemovalApprovalRecord>(e =>
        {
            e.ToTable("RemovalApprovals");
            e.HasIndex(a => a.RequestId);
            e.HasIndex(a => new { a.RequestId, a.ApprovedByUserId }).IsUnique();
            e.Property(a => a.IsVoid).HasDefaultValue(false);
            e.HasOne(a => a.Request)
             .WithMany(r => r.Approvals)
             .HasForeignKey(a => a.RequestId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.ApprovedByUser)
             .WithMany()
             .HasForeignKey(a => a.ApprovedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ViolationReportRecord>(e =>
        {
            e.ToTable("ViolationReports");
            e.HasIndex(v => v.ReportedUserId);
            e.HasIndex(v => v.Status);
            e.Property(v => v.Status).HasMaxLength(10).HasDefaultValue("pending");
            e.Property(v => v.ReportedByIp).HasMaxLength(50);
            e.Property(v => v.Explanation).HasMaxLength(2000);
            e.HasOne(v => v.ReportedUser)
             .WithMany()
             .HasForeignKey(v => v.ReportedUserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.ReportedByUser)
             .WithMany()
             .HasForeignKey(v => v.ReportedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.ReviewedByUser)
             .WithMany()
             .HasForeignKey(v => v.ReviewedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
