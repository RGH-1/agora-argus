using Agora.Web.Controllers;
using System.ComponentModel.DataAnnotations;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Forums;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraForumsTests
{
    [Fact]
    public async Task New_forum_is_pending_and_only_approved_forums_are_public()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("owner");
        var pending = await fixture.Service.CreateAsync("Pending forum", "Awaiting review", user.Id);
        var approved = await fixture.Service.CreateAsync("Approved forum", "Visible publicly", user.Id);
        approved.Status = ForumStatus.Approved;
        await fixture.Context.SaveChangesAsync();

        var publicForums = await fixture.Service.GetPublicAsync();
        Assert.Equal(ForumStatus.Pending, pending.Status);
        Assert.Single(publicForums);
        Assert.Equal(approved.Id, publicForums[0].Id);
        Assert.Null(await fixture.Service.GetPublicByIdAsync(pending.Id));
    }

    [Fact]
    public async Task Owners_see_all_statuses_but_other_users_cannot_modify_or_delete()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync("owner");
        var other = await fixture.CreateUserAsync("other");
        var forum = await fixture.Service.CreateAsync("Owner forum", "Original", owner.Id);
        var rejected = await fixture.Service.CreateAsync("Rejected forum", "Rejected", owner.Id);
        rejected.Status = ForumStatus.Rejected;
        await fixture.Context.SaveChangesAsync();

        var mine = await fixture.Service.GetMineAsync(owner.Id);
        Assert.Equal(2, mine.Count);
        await Assert.ThrowsAsync<ForumNotFoundOrForbiddenException>(() => fixture.Service.UpdateDescriptionAsync(forum.Id, "Nope", other.Id));
        await Assert.ThrowsAsync<ForumNotFoundOrForbiddenException>(() => fixture.Service.DeleteAsync(forum.Id, other.Id));

        await fixture.Service.UpdateDescriptionAsync(forum.Id, "Updated", owner.Id);
        Assert.Equal("Updated", (await fixture.Context.Forums.SingleAsync(item => item.Id == forum.Id)).Description);
        await fixture.Service.DeleteAsync(forum.Id, owner.Id);
        Assert.Null(await fixture.Context.Forums.FindAsync(forum.Id));
    }

    [Fact]
    public async Task Free_users_are_limited_to_three_including_pending_forums()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var freeUser = await fixture.CreateUserAsync("free");
        for (var i = 1; i <= 3; i++)
        {
            var forum = await fixture.Service.CreateAsync($"Free {i}", "Pending forum", freeUser.Id);
            Assert.Equal(ForumStatus.Pending, forum.Status);
        }

        var exception = await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.CreateAsync("Free four", "Too many", freeUser.Id));
        Assert.Contains("up to 3", exception.Message);
        Assert.Equal(3, await fixture.Context.Forums.CountAsync(item => item.CreatorId == freeUser.Id));
    }

    [Fact]
    public async Task Pro_users_can_create_more_than_three_forums()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var proUser = await fixture.CreateUserAsync("pro", isPro: true);
        for (var i = 1; i <= 4; i++) await fixture.Service.CreateAsync($"Pro {i}", "Unlimited", proUser.Id);

        Assert.Equal(4, await fixture.Context.Forums.CountAsync(item => item.CreatorId == proUser.Id));
    }

    [Fact]
    public async Task Invalid_and_case_insensitive_duplicate_names_are_rejected()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("owner");
        await fixture.Service.CreateAsync("Community Hub", "First", user.Id);

        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.CreateAsync(" community hub ", "Duplicate", user.Id));
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.CreateAsync(" ", "Description", user.Id));
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.CreateAsync(new string('x', 101), "Description", user.Id));
        var invalidModel = new CreateForumViewModel { Name = "", Description = new string('x', 501) };
        Assert.False(Validator.TryValidateObject(invalidModel, new ValidationContext(invalidModel), [], true));
    }

    [Fact]
    public void Controller_actions_are_authorized_and_forms_cannot_bind_owner_or_status()
    {
        var protectedActions = new[] { "Mine", "Create", "Edit", "Delete", "DeleteConfirmed" };
        foreach (var action in protectedActions)
        {
            var methods = typeof(ForumsController).GetMethods().Where(method => method.Name == action);
            Assert.All(methods, method => Assert.NotEmpty(method.GetCustomAttributes(typeof(AuthorizeAttribute), true)));
        }
        var deletePost = typeof(ForumsController).GetMethod("DeleteConfirmed")!;
        Assert.NotNull(deletePost.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(deletePost.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        var clientFields = typeof(CreateForumViewModel).GetProperties().Concat(typeof(EditForumViewModel).GetProperties()).Select(property => property.Name);
        Assert.DoesNotContain(clientFields, field => field is "CreatorId" or "Status" or "IsPro" or "NormalizedName");
    }

    private sealed class ForumFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private ForumFixture(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
            _scope = provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<AgoraDbContext>();
            Service = _scope.ServiceProvider.GetRequiredService<IForumService>();
        }

        public AgoraDbContext Context { get; }
        public IForumService Service { get; }

        public static async Task<ForumFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IForumRepository, ForumRepository>();
            services.AddScoped<IForumService, ForumService>();
            var provider = services.BuildServiceProvider();
            var fixture = new ForumFixture(connection, provider);
            await fixture.Context.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<ApplicationUser> CreateUserAsync(string name, bool isPro = false)
        {
            var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name, IsPro = isPro };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _scope.Dispose();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
