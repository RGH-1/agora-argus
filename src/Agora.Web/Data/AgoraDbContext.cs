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
    public DbSet<Rule> Rules => Set<Rule>();

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
    }
}
