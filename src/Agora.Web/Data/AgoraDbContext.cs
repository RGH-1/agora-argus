using Agora.Web.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Data;

public class AgoraDbContext(DbContextOptions<AgoraDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Forum> Forums => Set<Forum>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<UserWarning> UserWarnings => Set<UserWarning>();
    public DbSet<UserBanAction> UserBanActions => Set<UserBanAction>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<RuleSeedState> RuleSeedStates => Set<RuleSeedState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.Bio).HasMaxLength(500);
        });

        builder.Entity<Forum>(entity =>
        {
            entity.Property(forum => forum.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(forum => forum.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(forum => forum.RejectionReason).HasMaxLength(500);
            entity.HasIndex(forum => forum.Status);
            entity.HasIndex(forum => forum.CreatorId);
            entity.HasIndex(forum => forum.NormalizedName).IsUnique();
            entity.HasOne(forum => forum.Creator).WithMany(user => user.CreatedForums)
                .HasForeignKey(forum => forum.CreatorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Post>(entity =>
        {
            entity.Property(post => post.Title).HasMaxLength(150).IsRequired();
            entity.HasIndex(post => new { post.ForumId, post.CreatedAtUtc });
            entity.HasIndex(post => post.AuthorId);
            entity.HasOne(post => post.Forum).WithMany(forum => forum.Posts)
                .HasForeignKey(post => post.ForumId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(post => post.Author).WithMany(user => user.Posts)
                .HasForeignKey(post => post.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Comment>(entity =>
        {
            entity.HasIndex(comment => new { comment.PostId, comment.CreatedAtUtc });
            entity.HasOne(comment => comment.Post).WithMany(post => post.Comments)
                .HasForeignKey(comment => comment.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(comment => comment.Author).WithMany(user => user.Comments)
                .HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Like>(entity =>
        {
            entity.HasIndex(like => new { like.UserId, like.PostId }).IsUnique();
            entity.HasOne(like => like.Post).WithMany(post => post.Likes)
                .HasForeignKey(like => like.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(like => like.User).WithMany(user => user.Likes)
                .HasForeignKey(like => like.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Report>(entity =>
        {
            entity.Property(report => report.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(report => report.AiConfidence).HasPrecision(5, 4);
            entity.HasIndex(report => new { report.Status, report.CreatedAtUtc });
            entity.HasIndex(report => report.ReportingUserId);
            entity.HasIndex(report => new { report.ReportingUserId, report.PostId }).IsUnique().HasFilter("[Status] = 'Open'");
            entity.HasOne(report => report.Post).WithMany(post => post.Reports)
                .HasForeignKey(report => report.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(report => report.ReportingUser).WithMany(user => user.Reports)
                .HasForeignKey(report => report.ReportingUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Rule>(entity =>
        {
            entity.HasIndex(rule => rule.DisplayOrder).IsUnique();
        });

        builder.Entity<RuleSeedState>(entity => entity.HasKey(state => state.Id));

        builder.Entity<ModerationAction>(entity =>
        {
            entity.Property(action => action.ActionType).HasConversion<string>().HasMaxLength(30);
            entity.Property(action => action.Reason).HasMaxLength(500);
            entity.HasIndex(action => new { action.ReportId, action.CreatedAtUtc });
            entity.HasIndex(action => action.TargetAuthorId);
            entity.HasOne(action => action.Report).WithMany().HasForeignKey(action => action.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(action => action.Post).WithMany().HasForeignKey(action => action.PostId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(action => action.TargetAuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(action => action.ModeratorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserWarning>(entity =>
        {
            entity.Property(warning => warning.Message).HasMaxLength(500).IsRequired();
            entity.HasIndex(warning => new { warning.UserId, warning.CreatedAtUtc });
            entity.HasOne(warning => warning.Report).WithMany().HasForeignKey(warning => warning.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(warning => warning.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(warning => warning.ModeratorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserBanAction>(entity =>
        {
            entity.Property(action => action.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(action => new { action.TargetUserId, action.CreatedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(action => action.ActorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(action => action.TargetUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
