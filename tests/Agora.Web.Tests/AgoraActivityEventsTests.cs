using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Argus.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Tests;

public sealed class AgoraActivityEventsTests
{
    [Fact]
    public async Task Forum_creation_and_moderation_emit_exact_events_only_after_success()
    {
        await using var fixture = await Fixture.CreateAsync();
        var creator = await fixture.UserAsync("creator");
        var forums = new ForumService(new ForumRepository(fixture.Context), null, fixture.Tracker);

        var forum = await forums.CreateAsync("Analytics forum", "Description", creator.Id);
        await forums.ApproveAsync(forum.Id);
        await Assert.ThrowsAsync<ForumModerationException>(() => forums.ApproveAsync(forum.Id));

        Assert.Collection(fixture.Tracker.Events,
            item => AssertEvent(item, "Forum.Added", "Analytics forum", forum.Id.ToString(), "Analytics forum", creator.Id),
            item => AssertEvent(item, "forum.approved", "Analytics forum", forum.Id.ToString(), "Analytics forum", null));
    }

    [Fact]
    public async Task Post_comment_like_unlike_and_author_removal_emit_the_expected_metadata()
    {
        await using var fixture = await Fixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var commenter = await fixture.UserAsync("commenter"); var liker = await fixture.UserAsync("liker");
        var forum = await fixture.ForumAsync(author, "Analytics forum", ForumStatus.Approved);
        var posts = new PostService(new PostRepository(fixture.Context), null, fixture.Tracker);
        var post = await posts.CreateAsync(forum.Id, "Useful title", "Content", author.Id);
        await new CommentService(new CommentRepository(fixture.Context), fixture.Tracker).CreateAsync(post.Id, "Comment", commenter.Id);
        var likes = new LikesService(new LikesRepository(fixture.Context), null, fixture.Tracker);
        await likes.ToggleAsync(post.Id, liker.Id); await likes.ToggleAsync(post.Id, liker.Id);
        await posts.RemoveAsync(post.Id, author.Id);

        Assert.Collection(fixture.Tracker.Events,
            item => AssertEvent(item, "Post.Added", forum.Name, post.Id.ToString(), post.Title, author.Id),
            item => AssertEvent(item, "Comment.Added", forum.Name, post.Id.ToString(), post.Title, commenter.Id),
            item => AssertEvent(item, "post.liked", forum.Name, post.Id.ToString(), post.Title, liker.Id),
            item => AssertEvent(item, "post.unliked", forum.Name, post.Id.ToString(), post.Title, liker.Id),
            item => AssertEvent(item, "content.removed", forum.Name, post.Id.ToString(), post.Title, author.Id));
    }

    [Fact]
    public async Task Reports_moderation_and_pending_count_track_only_successful_operations()
    {
        await using var fixture = await Fixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter"); var moderator = await fixture.UserAsync("moderator");
        var forum = await fixture.ForumAsync(author, "Reported forum", ForumStatus.Approved);
        var post = await fixture.PostAsync(forum, author, "Reported title");
        var reports = new ReportService(new ReportRepository(fixture.Context), null, null, fixture.Tracker);

        var report = await reports.CreateAsync(post.Id, "Spam or misleading content", null, reporter.Id);
        Assert.Equal(1m, (decimal)await fixture.Context.Reports.CountAsync(item => item.Status == ReportStatus.Open));
        await Assert.ThrowsAsync<OpenReportAlreadyExistsException>(() => reports.CreateAsync(post.Id, "Spam or misleading content", null, reporter.Id));
        await reports.ResolveAsync(report.Id, moderator.Id, ModerationActionType.RemovePost, null);

        Assert.Equal(0m, (decimal)await fixture.Context.Reports.CountAsync(item => item.Status == ReportStatus.Open));
        Assert.Collection(fixture.Tracker.Events,
            item => AssertEvent(item, "content.reported", forum.Name, post.Id.ToString(), post.Title, reporter.Id),
            item => AssertEvent(item, "content.removed", forum.Name, post.Id.ToString(), post.Title, moderator.Id));
    }

    private static void AssertEvent(Activity item, string type, string? dimension, string? targetId, string? label, string? actor)
    { Assert.Equal(type, item.Type); Assert.Equal(1m, item.Value); Assert.Equal(dimension, item.Dimension); Assert.Equal(targetId, item.TargetId); Assert.Equal(label, item.Label); Assert.Equal(actor, item.Actor); }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private Fixture(SqliteConnection connection, AgoraDbContext context) { this.connection = connection; Context = context; }
        public AgoraDbContext Context { get; } = null!;
        public Tracker Tracker { get; } = new();
        public static async Task<Fixture> CreateAsync() { var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var context = new AgoraDbContext(new DbContextOptionsBuilder<AgoraDbContext>().UseSqlite(connection).Options); await context.Database.EnsureCreatedAsync(); return new Fixture(connection, context); }
        public async Task<ApplicationUser> UserAsync(string name) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name }; Context.Users.Add(user); await Context.SaveChangesAsync(); return user; }
        public async Task<Forum> ForumAsync(ApplicationUser user, string name, ForumStatus status) { var now = DateTime.UtcNow; var forum = new Forum { Name = name, NormalizedName = name.ToUpperInvariant(), Description = "Description", CreatorId = user.Id, Status = status, CreatedAtUtc = now, SubmittedAtUtc = now, UpdatedAtUtc = now }; Context.Forums.Add(forum); await Context.SaveChangesAsync(); return forum; }
        public async Task<Post> PostAsync(Forum forum, ApplicationUser author, string title) { var now = DateTime.UtcNow; var post = new Post { ForumId = forum.Id, AuthorId = author.Id, Title = title, Content = "Content", CreatedAtUtc = now, UpdatedAtUtc = now }; Context.Posts.Add(post); await Context.SaveChangesAsync(); return post; }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Tracker : IArgusTracker { public List<Activity> Events { get; } = []; public Task TrackAsync(string type, decimal value = 1, string? dimension = null, string? targetId = null, string? label = null, string? actor = null) { Events.Add(new(type, value, dimension, targetId, label, actor)); return Task.CompletedTask; } }
    private sealed record Activity(string Type, decimal Value, string? Dimension, string? TargetId, string? Label, string? Actor);
}
