using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Tests;

public sealed class AgoraRealtimeNotificationTests
{
    [Fact]
    public async Task Pending_forum_and_report_notify_only_moderators_after_persistence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var owner = await fixture.UserAsync("owner"); var reporter = await fixture.UserAsync("reporter");
        var forums = new ForumService(new ForumRepository(fixture.Context), null, null, fixture.Notifications);
        var forum = await forums.CreateAsync("Pending forum", "Description", owner.Id);
        forum.Status = ForumStatus.Approved; await fixture.Context.SaveChangesAsync();
        var post = await fixture.PostAsync(forum, owner);
        var reports = new ReportService(new ReportRepository(fixture.Context), null, null, null, fixture.Notifications);
        await reports.CreateAsync(post.Id, "Other", null, reporter.Id);

        Assert.Contains(fixture.Notifications.Messages, item => item == $"pending-forum:{forum.Id}");
        Assert.Contains(fixture.Notifications.Messages, item => item.StartsWith("pending-report:"));
        Assert.DoesNotContain(fixture.Notifications.Messages, item => item.Contains(owner.Id));
    }

    [Fact]
    public async Task Warning_and_ban_notify_only_the_affected_author_once_after_resolution()
    {
        await using var fixture = await Fixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter"); var moderator = await fixture.UserAsync("moderator");
        var forum = await fixture.ForumAsync(author); var post = await fixture.PostAsync(forum, author);
        var service = new ReportService(new ReportRepository(fixture.Context), null, null, null, fixture.Notifications);
        var warning = await service.CreateAsync(post.Id, "Other", null, reporter.Id);
        await service.ResolveAsync(warning.Id, moderator.Id, ModerationActionType.WarnAuthor, "Please follow the rules.");
        var secondReporter = await fixture.UserAsync("reporter2");
        var ban = await service.CreateAsync(post.Id, "Other", null, secondReporter.Id);
        await service.ResolveAsync(ban.Id, moderator.Id, ModerationActionType.BanAuthor, "Repeated violations.");

        Assert.Equal(1, fixture.Notifications.Messages.Count(item => item == $"warning:{author.Id}"));
        Assert.Equal(1, fixture.Notifications.Messages.Count(item => item == $"ban:{author.Id}"));
        Assert.DoesNotContain(fixture.Notifications.Messages, item => item == $"warning:{reporter.Id}" || item == $"ban:{moderator.Id}");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private Fixture(SqliteConnection connection, AgoraDbContext context) { this.connection = connection; Context = context; }
        public AgoraDbContext Context { get; } = null!;
        public Notifications Notifications { get; } = new();
        public static async Task<Fixture> CreateAsync() { var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var context = new AgoraDbContext(new DbContextOptionsBuilder<AgoraDbContext>().UseSqlite(connection).Options); await context.Database.EnsureCreatedAsync(); return new Fixture(connection, context); }
        public async Task<ApplicationUser> UserAsync(string name) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name }; Context.Users.Add(user); await Context.SaveChangesAsync(); return user; }
        public async Task<Forum> ForumAsync(ApplicationUser owner) { var now = DateTime.UtcNow; var forum = new Forum { Name = "Forum", NormalizedName = "FORUM", Description = "Description", CreatorId = owner.Id, Status = ForumStatus.Approved, CreatedAtUtc = now, SubmittedAtUtc = now, UpdatedAtUtc = now }; Context.Forums.Add(forum); await Context.SaveChangesAsync(); return forum; }
        public async Task<Post> PostAsync(Forum forum, ApplicationUser author) { var now = DateTime.UtcNow; var post = new Post { ForumId = forum.Id, AuthorId = author.Id, Title = "Post", Content = "Content", CreatedAtUtc = now, UpdatedAtUtc = now }; Context.Posts.Add(post); await Context.SaveChangesAsync(); return post; }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Notifications : IRealtimeNotificationService
    {
        public List<string> Messages { get; } = [];
        public Task PendingForumCreatedAsync(int id, string name) { Messages.Add($"pending-forum:{id}"); return Task.CompletedTask; }
        public Task ForumApprovedAsync(string id, int forumId, string name) { Messages.Add($"approved:{id}"); return Task.CompletedTask; }
        public Task ForumRejectedAsync(string id, int forumId, string name) { Messages.Add($"rejected:{id}"); return Task.CompletedTask; }
        public Task PendingReportCreatedAsync(int id) { Messages.Add($"pending-report:{id}"); return Task.CompletedTask; }
        public Task PostRemovedAsync(string id, int postId, string title) { Messages.Add($"removed:{id}"); return Task.CompletedTask; }
        public Task WarningIssuedAsync(string id) { Messages.Add($"warning:{id}"); return Task.CompletedTask; }
        public Task UserBannedAsync(string id) { Messages.Add($"ban:{id}"); return Task.CompletedTask; }
        public Task ModerationQueueChangedAsync(string queue) { Messages.Add($"queue:{queue}"); return Task.CompletedTask; }
    }
}
