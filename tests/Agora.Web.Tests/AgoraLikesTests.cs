using Agora.Web.Controllers;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraLikesTests
{
    [Fact]
    public async Task Authenticated_user_toggle_creates_one_like_then_removes_it_and_updates_count()
    {
        await using var fixture = await LikeFixture.CreateAsync();
        var user = await fixture.UserAsync("member");
        var post = await fixture.PublicPostAsync(user.Id);

        await fixture.Service.ToggleAsync(post.Id, user.Id);
        var liked = await fixture.Service.GetStatusForPublicPostAsync(post.Id, user.Id);
        Assert.NotNull(liked); Assert.True(liked.IsLiked); Assert.Equal(1, liked.Count);
        Assert.Single(await fixture.Context.Likes.Where(like => like.PostId == post.Id && like.UserId == user.Id).ToListAsync());

        await fixture.Service.ToggleAsync(post.Id, user.Id);
        var unliked = await fixture.Service.GetStatusForPublicPostAsync(post.Id, user.Id);
        Assert.NotNull(unliked); Assert.False(unliked.IsLiked); Assert.Equal(0, unliked.Count);
    }

    [Fact]
    public async Task Multiple_users_and_forum_post_counts_are_accurate_and_isolated_per_post()
    {
        await using var fixture = await LikeFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var first = await fixture.UserAsync("first"); var second = await fixture.UserAsync("second");
        var forum = await fixture.ForumAsync(ForumStatus.Approved, author.Id);
        var firstPost = await fixture.PostAsync(forum.Id, author.Id, "First");
        var secondPost = await fixture.PostAsync(forum.Id, author.Id, "Second");

        await fixture.Service.ToggleAsync(firstPost.Id, first.Id);
        await fixture.Service.ToggleAsync(firstPost.Id, second.Id);
        var counts = await fixture.Service.GetCountsForPostsAsync([firstPost.Id, secondPost.Id]);

        Assert.Equal(2, counts[firstPost.Id]);
        Assert.False(counts.ContainsKey(secondPost.Id));
        Assert.Equal(2, (await fixture.Service.GetStatusForPublicPostAsync(firstPost.Id, first.Id))!.Count);
        Assert.False((await fixture.Service.GetStatusForPublicPostAsync(firstPost.Id, author.Id))!.IsLiked);
    }

    [Fact]
    public async Task Unique_index_rejects_duplicate_like_rows()
    {
        await using var fixture = await LikeFixture.CreateAsync();
        var user = await fixture.UserAsync("member"); var post = await fixture.PublicPostAsync(user.Id);
        fixture.Context.Likes.Add(new Like { PostId = post.Id, UserId = user.Id });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.Likes.Add(new Like { PostId = post.Id, UserId = user.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_duplicate_insert_conflict_is_handled_without_an_unhandled_exception()
    {
        var service = new LikesService(new ConcurrentConflictRepository());

        await service.ToggleAsync(1, "member");
    }

    [Fact]
    public async Task Missing_removed_pending_and_rejected_posts_cannot_be_liked()
    {
        await using var fixture = await LikeFixture.CreateAsync();
        var user = await fixture.UserAsync("member");
        var removed = await fixture.PublicPostAsync(user.Id); removed.IsRemoved = true;
        var pending = await fixture.PostAsync((await fixture.ForumAsync(ForumStatus.Pending, user.Id)).Id, user.Id, "Pending");
        var rejected = await fixture.PostAsync((await fixture.ForumAsync(ForumStatus.Rejected, user.Id)).Id, user.Id, "Rejected");
        await fixture.Context.SaveChangesAsync();

        foreach (var postId in new[] { 999, removed.Id, pending.Id, rejected.Id })
            await Assert.ThrowsAsync<LikeNotFoundOrForbiddenException>(() => fixture.Service.ToggleAsync(postId, user.Id));
        Assert.Empty(await fixture.Context.Likes.ToListAsync());
    }

    [Fact]
    public void Toggle_endpoint_is_post_authorized_antiforgery_protected_and_does_not_bind_user_id()
    {
        Assert.NotEmpty(typeof(LikesController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var method = typeof(LikesController).GetMethod(nameof(LikesController.Toggle))!;
        Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        Assert.DoesNotContain(method.GetParameters(), parameter => parameter.Name?.Equals("userId", StringComparison.OrdinalIgnoreCase) == true);
    }

    private sealed class LikeFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection; private readonly ServiceProvider provider; private readonly IServiceScope scope;
        private LikeFixture(SqliteConnection connection, ServiceProvider provider) { this.connection = connection; this.provider = provider; scope = provider.CreateScope(); Context = scope.ServiceProvider.GetRequiredService<AgoraDbContext>(); Service = scope.ServiceProvider.GetRequiredService<ILikesService>(); }
        public AgoraDbContext Context { get; } public ILikesService Service { get; }
        public static async Task<LikeFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection(); services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<ILikesRepository, LikesRepository>(); services.AddScoped<ILikesService, LikesService>();
            var fixture = new LikeFixture(connection, services.BuildServiceProvider()); await fixture.Context.Database.EnsureCreatedAsync(); return fixture;
        }
        public async Task<ApplicationUser> UserAsync(string name) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name }; Context.Users.Add(user); await Context.SaveChangesAsync(); return user; }
        public async Task<Forum> ForumAsync(ForumStatus status, string creatorId) { var suffix = Guid.NewGuid().ToString("N"); var forum = new Forum { Name = $"Forum {suffix}", NormalizedName = suffix.ToUpperInvariant(), Description = "Forum", Status = status, CreatorId = creatorId }; Context.Forums.Add(forum); await Context.SaveChangesAsync(); return forum; }
        public async Task<Post> PostAsync(int forumId, string authorId, string title) { var post = new Post { ForumId = forumId, AuthorId = authorId, Title = title, Content = "Content" }; Context.Posts.Add(post); await Context.SaveChangesAsync(); return post; }
        public async Task<Post> PublicPostAsync(string authorId) => await PostAsync((await ForumAsync(ForumStatus.Approved, authorId)).Id, authorId, "Public");
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); scope.Dispose(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class ConcurrentConflictRepository : ILikesRepository
    {
        public Task<Post?> GetPublicPostAsync(int postId, CancellationToken cancellationToken = default) => Task.FromResult<Post?>(new Post { Id = postId });
        public Task<LikeStatus?> GetStatusForPublicPostAsync(int postId, string? userId, CancellationToken cancellationToken = default) => Task.FromResult<LikeStatus?>(null);
        public Task<IReadOnlyDictionary<int, int>> GetCountsForPostsAsync(IEnumerable<int> postIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
        public Task<bool> TryRemoveAsync(int postId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<LikeAddResult> AddAsync(int postId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(LikeAddResult.AlreadyExists);
    }
}
