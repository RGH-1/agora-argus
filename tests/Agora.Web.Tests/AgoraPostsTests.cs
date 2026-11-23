using System.ComponentModel.DataAnnotations;
using Agora.Web.Controllers;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Posts;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraPostsTests
{
    [Fact]
    public async Task Approved_forum_creates_post_with_identity_author_and_utc_timestamps()
    {
        await using var fixture = await PostFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var forum = await fixture.ForumAsync("Approved", ForumStatus.Approved, author.Id);
        var post = await fixture.Service.CreateAsync(forum.Id, " A title ", " A body ", author.Id);

        Assert.Equal(author.Id, post.AuthorId);
        Assert.Equal(forum.Id, post.ForumId);
        Assert.Equal("A title", post.Title);
        Assert.Equal(DateTimeKind.Utc, post.CreatedAtUtc.Kind);
        Assert.Equal(post.CreatedAtUtc, post.UpdatedAtUtc);
    }

    [Fact]
    public async Task Pending_rejected_and_missing_forums_reject_post_creation()
    {
        await using var fixture = await PostFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var pending = await fixture.ForumAsync("Pending", ForumStatus.Pending, author.Id);
        var rejected = await fixture.ForumAsync("Rejected", ForumStatus.Rejected, author.Id);

        await Assert.ThrowsAsync<PostBusinessRuleException>(() => fixture.Service.CreateAsync(pending.Id, "Title", "Body", author.Id));
        await Assert.ThrowsAsync<PostBusinessRuleException>(() => fixture.Service.CreateAsync(rejected.Id, "Title", "Body", author.Id));
        await Assert.ThrowsAsync<PostNotFoundOrForbiddenException>(() => fixture.Service.CreateAsync(999, "Title", "Body", author.Id));
    }

    [Fact]
    public async Task Validation_and_case_direct_requests_cannot_bypass_post_rules()
    {
        await using var fixture = await PostFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var forum = await fixture.ForumAsync("Approved", ForumStatus.Approved, author.Id);
        foreach (var input in new[] { ("", "body"), ("  ", "body"), ("title", ""), ("title", "  "), (new string('x', 151), "body"), ("title", new string('x', 4001)) })
            await Assert.ThrowsAsync<PostBusinessRuleException>(() => fixture.Service.CreateAsync(forum.Id, input.Item1, input.Item2, author.Id));
        var invalid = new CreatePostViewModel { Title = "", Content = new string('x', 4001) };
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true));
    }

    [Fact]
    public async Task Exact_limits_are_accepted_and_over_limit_input_is_never_truncated_or_saved()
    {
        await using var fixture = await PostFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var forum = await fixture.ForumAsync("Approved", ForumStatus.Approved, author.Id);
        var maxTitle = new string('t', 150);
        var maxContent = new string('c', 4000);
        var post = await fixture.Service.CreateAsync(forum.Id, maxTitle, maxContent, author.Id);

        Assert.Equal(150, post.Title.Length);
        Assert.Equal(4000, post.Content.Length);
        var tooLongTitle = new string('t', 151);
        var tooLongContent = new string('c', 4001);
        await Assert.ThrowsAsync<PostBusinessRuleException>(() => fixture.Service.CreateAsync(forum.Id, tooLongTitle, "Valid", author.Id));
        await Assert.ThrowsAsync<PostBusinessRuleException>(() => fixture.Service.CreateAsync(forum.Id, "Valid", tooLongContent, author.Id));
        await Assert.ThrowsAsync<PostBusinessRuleException>(() => fixture.Service.UpdateAsync(post.Id, tooLongTitle, tooLongContent, author.Id));

        var persisted = await fixture.Context.Posts.SingleAsync(item => item.Id == post.Id);
        Assert.Equal(maxTitle, persisted.Title);
        Assert.Equal(maxContent, persisted.Content);
        Assert.Equal(151, tooLongTitle.Length);
        Assert.Equal(4001, tooLongContent.Length);
    }

    [Fact]
    public async Task Public_forum_posts_are_newest_first_and_hide_removed_or_nonpublic_content()
    {
        await using var fixture = await PostFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var approved = await fixture.ForumAsync("Approved", ForumStatus.Approved, author.Id);
        var older = await fixture.Service.CreateAsync(approved.Id, "Older", "Body", author.Id);
        older.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var newer = await fixture.Service.CreateAsync(approved.Id, "Newer", "Body", author.Id);
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.RemoveAsync(older.Id, author.Id);

        var posts = await fixture.Service.GetPublicByForumAsync(approved.Id);
        Assert.Single(posts); Assert.Equal(newer.Id, posts[0].Id);
        Assert.Null(await fixture.Service.GetPublicByIdAsync(older.Id));
        approved.Status = ForumStatus.Pending; await fixture.Context.SaveChangesAsync();
        Assert.Empty(await fixture.Service.GetPublicByForumAsync(approved.Id));
        Assert.Null(await fixture.Service.GetPublicByIdAsync(newer.Id));
    }

    [Fact]
    public async Task Only_author_can_edit_or_soft_delete_and_created_timestamp_is_preserved()
    {
        await using var fixture = await PostFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var other = await fixture.UserAsync("other");
        var forum = await fixture.ForumAsync("Approved", ForumStatus.Approved, author.Id);
        var post = await fixture.Service.CreateAsync(forum.Id, "Original", "Original body", author.Id);
        var created = post.CreatedAtUtc;
        await Assert.ThrowsAsync<PostNotFoundOrForbiddenException>(() => fixture.Service.UpdateAsync(post.Id, "No", "No", other.Id));
        await fixture.Service.UpdateAsync(post.Id, "Updated", "Updated body", author.Id);
        Assert.Equal(created, post.CreatedAtUtc); Assert.True(post.UpdatedAtUtc >= created);
        await Assert.ThrowsAsync<PostNotFoundOrForbiddenException>(() => fixture.Service.RemoveAsync(post.Id, other.Id));
        await fixture.Service.RemoveAsync(post.Id, author.Id);
        Assert.True(post.IsRemoved);
        await Assert.ThrowsAsync<PostNotFoundOrForbiddenException>(() => fixture.Service.UpdateAsync(post.Id, "Again", "Again", author.Id));
    }

    [Fact]
    public void Write_routes_are_authorized_antiforgery_protected_and_models_exclude_sensitive_fields()
    {
        foreach (var name in new[] { "Create", "Edit", "Delete", "DeleteConfirmed" })
            Assert.All(typeof(PostsController).GetMethods().Where(method => method.Name == name), method => Assert.NotEmpty(method.GetCustomAttributes(typeof(AuthorizeAttribute), true)));
        foreach (var name in new[] { "Create", "Edit", "DeleteConfirmed" })
        {
            var method = typeof(PostsController).GetMethods().Single(method => method.Name == name && method.GetCustomAttributes(typeof(HttpPostAttribute), true).Any());
            Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        }
        var fields = typeof(CreatePostViewModel).GetProperties().Concat(typeof(EditPostViewModel).GetProperties()).Select(property => property.Name);
        Assert.DoesNotContain(fields, field => field is "AuthorId" or "IsRemoved" or "CreatedAtUtc" or "UpdatedAtUtc");
    }

    private sealed class PostFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection; private readonly ServiceProvider _provider; private readonly IServiceScope _scope;
        private PostFixture(SqliteConnection connection, ServiceProvider provider) { _connection = connection; _provider = provider; _scope = provider.CreateScope(); Context = _scope.ServiceProvider.GetRequiredService<AgoraDbContext>(); Service = _scope.ServiceProvider.GetRequiredService<IPostService>(); }
        public AgoraDbContext Context { get; } public IPostService Service { get; }
        public static async Task<PostFixture> CreateAsync() { var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); var services = new ServiceCollection(); services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection)); services.AddScoped<IPostRepository, PostRepository>(); services.AddScoped<IPostService, PostService>(); var fixture = new PostFixture(connection, services.BuildServiceProvider()); await fixture.Context.Database.EnsureCreatedAsync(); return fixture; }
        public async Task<ApplicationUser> UserAsync(string name) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name }; Context.Users.Add(user); await Context.SaveChangesAsync(); return user; }
        public async Task<Forum> ForumAsync(string name, ForumStatus status, string creatorId) { var forum = new Forum { Name = name, NormalizedName = name.ToUpperInvariant(), Description = "Forum", Status = status, CreatorId = creatorId }; Context.Forums.Add(forum); await Context.SaveChangesAsync(); return forum; }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); _scope.Dispose(); await _provider.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
