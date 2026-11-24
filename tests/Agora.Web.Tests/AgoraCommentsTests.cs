using System.ComponentModel.DataAnnotations;
using Agora.Web.Controllers;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Comments;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraCommentsTests
{
    [Fact]
    public async Task Public_post_comments_are_oldest_first_and_include_only_that_posts_author_data()
    {
        await using var fixture = await CommentFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var commenter = await fixture.UserAsync("commenter");
        var forum = await fixture.ForumAsync(ForumStatus.Approved, author.Id);
        var post = await fixture.PostAsync(forum.Id, author.Id);
        var otherPost = await fixture.PostAsync(forum.Id, author.Id);
        var newer = await fixture.Service.CreateAsync(post.Id, "Newer", commenter.Id);
        newer.CreatedAtUtc = DateTime.UtcNow;
        var older = await fixture.Service.CreateAsync(post.Id, "Older", author.Id);
        older.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Service.CreateAsync(otherPost.Id, "Other post", commenter.Id);
        await fixture.Context.SaveChangesAsync();

        var comments = await fixture.Service.GetForPublicPostAsync(post.Id);

        Assert.Collection(comments,
            comment => { Assert.Equal("Older", comment.Content); Assert.Equal(author.DisplayName, comment.Author.DisplayName); },
            comment => { Assert.Equal("Newer", comment.Content); Assert.Equal(commenter.DisplayName, comment.Author.DisplayName); });
        Assert.All(comments, comment => Assert.Equal(post.Id, comment.PostId));
        Assert.All(comments, comment => Assert.NotEqual(default, comment.CreatedAtUtc));
    }

    [Fact]
    public async Task Content_validation_accepts_2000_rejects_whitespace_and_never_truncates()
    {
        await using var fixture = await CommentFixture.CreateAsync();
        var user = await fixture.UserAsync("member");
        var post = await fixture.PublicPostAsync(user.Id);
        var maximum = new string('c', 2000);
        var tooLong = new string('c', 2001);

        var comment = await fixture.Service.CreateAsync(post.Id, $" {maximum} ", user.Id);

        Assert.Equal(maximum, comment.Content);
        await Assert.ThrowsAsync<CommentBusinessRuleException>(() => fixture.Service.CreateAsync(post.Id, " ", user.Id));
        await Assert.ThrowsAsync<CommentBusinessRuleException>(() => fixture.Service.CreateAsync(post.Id, tooLong, user.Id));
        Assert.Equal(2001, tooLong.Length);
        Assert.Single(await fixture.Service.GetForPublicPostAsync(post.Id));
        var invalid = new CreateCommentViewModel { Content = "  " };
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true));
    }

    [Fact]
    public async Task Removed_nonpublic_and_banned_cases_cannot_create_comments()
    {
        await using var fixture = await CommentFixture.CreateAsync();
        var member = await fixture.UserAsync("member");
        var banned = await fixture.UserAsync("banned", true);
        var removed = await fixture.PublicPostAsync(member.Id); removed.IsRemoved = true;
        var pendingForum = await fixture.ForumAsync(ForumStatus.Pending, member.Id);
        var pendingPost = await fixture.PostAsync(pendingForum.Id, member.Id);
        var rejectedForum = await fixture.ForumAsync(ForumStatus.Rejected, member.Id);
        var rejectedPost = await fixture.PostAsync(rejectedForum.Id, member.Id);
        var publicPost = await fixture.PublicPostAsync(member.Id);
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<CommentNotFoundOrForbiddenException>(() => fixture.Service.CreateAsync(removed.Id, "No", member.Id));
        await Assert.ThrowsAsync<CommentNotFoundOrForbiddenException>(() => fixture.Service.CreateAsync(pendingPost.Id, "No", member.Id));
        await Assert.ThrowsAsync<CommentNotFoundOrForbiddenException>(() => fixture.Service.CreateAsync(rejectedPost.Id, "No", member.Id));
        await Assert.ThrowsAsync<CommentBusinessRuleException>(() => fixture.Service.CreateAsync(publicPost.Id, "No", banned.Id));
        Assert.Empty(await fixture.Service.GetForPublicPostAsync(removed.Id));
        Assert.Empty(await fixture.Service.GetForPublicPostAsync(pendingPost.Id));
        Assert.Empty(await fixture.Service.GetForPublicPostAsync(rejectedPost.Id));
    }

    [Fact]
    public async Task Authors_and_privileged_moderators_can_delete_but_other_members_cannot()
    {
        await using var fixture = await CommentFixture.CreateAsync();
        var author = await fixture.UserAsync("author");
        var member = await fixture.UserAsync("member");
        var moderator = await fixture.UserAsync("moderator");
        var admin = await fixture.UserAsync("admin");
        var post = await fixture.PublicPostAsync(author.Id);
        var own = await fixture.Service.CreateAsync(post.Id, "Own", author.Id);
        var moderation = await fixture.Service.CreateAsync(post.Id, "Moderate", author.Id);
        var administration = await fixture.Service.CreateAsync(post.Id, "Admin", author.Id);

        await Assert.ThrowsAsync<CommentNotFoundOrForbiddenException>(() => fixture.Service.DeleteAsync(own.Id, member.Id, false));
        await fixture.Service.DeleteAsync(own.Id, author.Id, false);
        await fixture.Service.DeleteAsync(moderation.Id, moderator.Id, true);
        await fixture.Service.DeleteAsync(administration.Id, admin.Id, true);
        Assert.Empty(await fixture.Service.GetForPublicPostAsync(post.Id));
    }

    [Fact]
    public void Comment_write_endpoints_require_authorization_antiforgery_and_have_no_reply_action()
    {
        var methods = typeof(CommentsController).GetMethods().Where(method => method.Name is "Create" or "Delete").ToList();
        Assert.Equal(2, methods.Count);
        Assert.All(methods, method =>
        {
            Assert.NotEmpty(method.GetCustomAttributes(typeof(AuthorizeAttribute), true));
            Assert.NotNull(method.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
            Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        });
    }

    private sealed class CommentFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection; private readonly ServiceProvider provider; private readonly IServiceScope scope;
        private CommentFixture(SqliteConnection connection, ServiceProvider provider) { this.connection = connection; this.provider = provider; scope = provider.CreateScope(); Context = scope.ServiceProvider.GetRequiredService<AgoraDbContext>(); Service = scope.ServiceProvider.GetRequiredService<ICommentService>(); }
        public AgoraDbContext Context { get; } public ICommentService Service { get; }
        public static async Task<CommentFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection(); services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<ICommentRepository, CommentRepository>(); services.AddScoped<ICommentService, CommentService>();
            var fixture = new CommentFixture(connection, services.BuildServiceProvider()); await fixture.Context.Database.EnsureCreatedAsync(); return fixture;
        }
        public async Task<ApplicationUser> UserAsync(string name, bool banned = false) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name, IsBanned = banned }; Context.Users.Add(user); await Context.SaveChangesAsync(); return user; }
        public async Task<Forum> ForumAsync(ForumStatus status, string creatorId) { var suffix = Guid.NewGuid().ToString("N"); var forum = new Forum { Name = $"Forum {suffix}", NormalizedName = suffix.ToUpperInvariant(), Description = "Forum", Status = status, CreatorId = creatorId }; Context.Forums.Add(forum); await Context.SaveChangesAsync(); return forum; }
        public async Task<Post> PostAsync(int forumId, string authorId) { var post = new Post { ForumId = forumId, AuthorId = authorId, Title = "Title", Content = "Content" }; Context.Posts.Add(post); await Context.SaveChangesAsync(); return post; }
        public async Task<Post> PublicPostAsync(string authorId) { var forum = await ForumAsync(ForumStatus.Approved, authorId); return await PostAsync(forum.Id, authorId); }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); scope.Dispose(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
