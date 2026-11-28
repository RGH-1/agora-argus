using Agora.Web.Controllers;
using ModerationForumsController = Agora.Web.Areas.Moderation.Controllers.ForumsController;
using System.ComponentModel.DataAnnotations;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Forums;
using Agora.Web.Models.Moderation;
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
    public void Reject_forum_model_enforces_empty_whitespace_and_length_limits_without_truncation()
    {
        static bool IsValid(string reason)
        {
            var model = new RejectForumViewModel { ForumId = 1, Reason = reason };
            return Validator.TryValidateObject(model, new ValidationContext(model), [], true);
        }

        Assert.True(IsValid(new string('r', 500)));
        Assert.False(IsValid(string.Empty));
        Assert.False(IsValid("   "));
        Assert.False(IsValid(new string('r', 501)));
    }

    [Fact]
    public void Reject_forum_client_script_initializes_counter_without_eager_validation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "agora-argus.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var script = File.ReadAllText(Path.Combine(root!.FullName, "src", "Agora.Web", "wwwroot", "js", "moderation-rejection.js"));
        Assert.Contains("updateCounter(); field.addEventListener", script);
        Assert.DoesNotContain("validate(); field.addEventListener", script);
        Assert.Contains("form.addEventListener(\"submit\"", script);
    }

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
        fixture.Context.ChangeTracker.Clear();
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
    public async Task Exact_forum_limits_are_accepted_and_over_limit_input_is_not_truncated_or_saved()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("owner");
        var maxName = new string('n', 100);
        var maxDescription = new string('d', 500);
        var forum = await fixture.Service.CreateAsync(maxName, maxDescription, user.Id);

        Assert.Equal(100, forum.Name.Length);
        Assert.Equal(500, forum.Description!.Length);
        var tooLongName = new string('n', 101);
        var tooLongDescription = new string('d', 501);
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.CreateAsync(tooLongName, "Valid", user.Id));
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.CreateAsync("Different forum", tooLongDescription, user.Id));
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.UpdateDescriptionAsync(forum.Id, tooLongDescription, user.Id));

        var persisted = await fixture.Context.Forums.SingleAsync(item => item.Id == forum.Id);
        Assert.Equal(maxName, persisted.Name);
        Assert.Equal(maxDescription, persisted.Description);
        Assert.Equal(101, tooLongName.Length);
        Assert.Equal(501, tooLongDescription.Length);
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

    [Fact]
    public async Task Moderation_queue_contains_only_pending_forums_oldest_first()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("owner");
        var newest = await fixture.Service.CreateAsync("Newest", "Description", user.Id);
        newest.SubmittedAtUtc = DateTime.UtcNow;
        var oldest = await fixture.Service.CreateAsync("Oldest", "Description", user.Id);
        oldest.SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var approved = await fixture.Service.CreateAsync("Approved", "Description", user.Id);
        approved.Status = ForumStatus.Approved;
        await fixture.Context.SaveChangesAsync();

        var queue = await fixture.Service.GetPendingAsync();
        Assert.Equal([oldest.Id, newest.Id], queue.Select(forum => forum.Id));
    }

    [Fact]
    public async Task Approval_and_rejection_are_pending_only_and_rejection_reason_is_preserved()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("owner");
        var approved = await fixture.Service.CreateAsync("To approve", "Description", user.Id);
        var rejected = await fixture.Service.CreateAsync("To reject", "Description", user.Id);

        await fixture.Service.ApproveAsync(approved.Id);
        await fixture.Service.RejectAsync(rejected.Id, "  Please clarify the community purpose.  ");

        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(ForumStatus.Approved, (await fixture.Context.Forums.FindAsync(approved.Id))!.Status);
        Assert.NotNull(await fixture.Service.GetPublicByIdAsync(approved.Id));
        var rejectedForum = await fixture.Context.Forums.FindAsync(rejected.Id);
        Assert.Equal(ForumStatus.Rejected, rejectedForum!.Status);
        Assert.Equal("Please clarify the community purpose.", rejectedForum.RejectionReason);
        await Assert.ThrowsAsync<ForumModerationException>(() => fixture.Service.ApproveAsync(rejected.Id));
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.RejectAsync(approved.Id, " "));
    }

    [Fact]
    public void Moderation_actions_are_server_authorized_and_post_protected()
    {
        var authorization = typeof(ModerationForumsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Moderator,Admin", authorization.Roles);
        foreach (var methodName in new[] { "Approve", "Reject" })
        {
            var method = typeof(ModerationForumsController).GetMethods().First(method => method.Name == methodName && method.GetCustomAttributes(typeof(HttpPostAttribute), true).Any());
            Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        }
    }

    [Fact]
    public async Task Changed_rejected_and_approved_forums_return_to_pending_without_losing_posts()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync("owner");
        var rejected = await fixture.Service.CreateAsync("Rejected lifecycle", "Original", owner.Id);
        await fixture.Service.RejectAsync(rejected.Id, "Needs clearer scope");
        fixture.Context.ChangeTracker.Clear();

        var resubmitted = await fixture.Service.UpdateDescriptionAsync(rejected.Id, "Corrected description", owner.Id);
        fixture.Context.ChangeTracker.Clear();
        var afterRejectedEdit = await fixture.Context.Forums.FindAsync(rejected.Id);
        Assert.Equal(ForumDescriptionUpdateResult.Updated, resubmitted);
        Assert.Equal(ForumStatus.Pending, afterRejectedEdit!.Status);
        Assert.Null(afterRejectedEdit.RejectionReason);
        Assert.Contains((await fixture.Service.GetPendingAsync()), forum => forum.Id == rejected.Id);

        await fixture.Service.ApproveAsync(rejected.Id);
        fixture.Context.Posts.Add(new Post { ForumId = rejected.Id, AuthorId = owner.Id, Content = "Preserved post" });
        await fixture.Context.SaveChangesAsync();
        var postCount = await fixture.Context.Posts.CountAsync(post => post.ForumId == rejected.Id);
        await fixture.Service.UpdateDescriptionAsync(rejected.Id, "Changed after approval", owner.Id);
        fixture.Context.ChangeTracker.Clear();
        var afterApprovedEdit = await fixture.Context.Forums.FindAsync(rejected.Id);
        Assert.Equal(ForumStatus.Pending, afterApprovedEdit!.Status);
        Assert.Null(await fixture.Service.GetPublicByIdAsync(rejected.Id));
        Assert.Equal(postCount, await fixture.Context.Posts.CountAsync(post => post.ForumId == rejected.Id));
        await fixture.Service.ApproveAsync(rejected.Id);
        Assert.NotNull(await fixture.Service.GetPublicByIdAsync(rejected.Id));
    }

    [Fact]
    public async Task Unchanged_or_invalid_description_preserves_lifecycle_state_and_reason()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync("owner");
        var forum = await fixture.Service.CreateAsync("No change", "Saved description", owner.Id);
        await fixture.Service.RejectAsync(forum.Id, "Specific reason");
        fixture.Context.ChangeTracker.Clear();
        var before = await fixture.Context.Forums.FindAsync(forum.Id);
        var timestamp = before!.UpdatedAtUtc;

        var result = await fixture.Service.UpdateDescriptionAsync(forum.Id, "  Saved description  ", owner.Id);
        fixture.Context.ChangeTracker.Clear();
        var unchanged = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.Equal(ForumDescriptionUpdateResult.Unchanged, result);
        Assert.Equal(ForumStatus.Rejected, unchanged!.Status);
        Assert.Equal("Specific reason", unchanged.RejectionReason);
        Assert.Equal(timestamp, unchanged.UpdatedAtUtc);
        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => fixture.Service.UpdateDescriptionAsync(forum.Id, " ", owner.Id));
        fixture.Context.ChangeTracker.Clear();
        var invalid = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.Equal(ForumStatus.Rejected, invalid!.Status);
        Assert.Equal("Specific reason", invalid.RejectionReason);
    }

    [Fact]
    public async Task Deleting_rejected_forum_frees_free_member_slot_and_atomic_edit_preserves_owner_protection()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync("owner");
        var other = await fixture.CreateUserAsync("other");
        var first = await fixture.Service.CreateAsync("First", "Description", owner.Id);
        var second = await fixture.Service.CreateAsync("Second", "Description", owner.Id);
        var third = await fixture.Service.CreateAsync("Third", "Description", owner.Id);
        await fixture.Service.RejectAsync(third.Id, "Reason");
        await Assert.ThrowsAsync<ForumNotFoundOrForbiddenException>(() => fixture.Service.UpdateDescriptionAsync(first.Id, "Hijack", other.Id));
        await fixture.Service.DeleteAsync(third.Id, owner.Id);
        var replacement = await fixture.Service.CreateAsync("Replacement", "Description", owner.Id);
        Assert.Equal(ForumStatus.Pending, replacement.Status);
        Assert.Equal(3, await fixture.Context.Forums.CountAsync(forum => forum.CreatorId == owner.Id));
    }

    [Fact]
    public async Task Submitted_at_tracks_new_and_resubmitted_forums_but_not_pending_or_moderator_updates()
    {
        await using var fixture = await ForumFixture.CreateAsync();
        var owner = await fixture.CreateUserAsync("owner");
        var forum = await fixture.Service.CreateAsync("Submission clock", "Original", owner.Id);
        Assert.Equal(forum.CreatedAtUtc, forum.SubmittedAtUtc);
        var originalSubmission = forum.SubmittedAtUtc;

        await fixture.Service.UpdateDescriptionAsync(forum.Id, "Pending edit", owner.Id);
        fixture.Context.ChangeTracker.Clear();
        var pending = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.Equal(originalSubmission, pending!.SubmittedAtUtc);
        await fixture.Service.ApproveAsync(forum.Id);
        fixture.Context.ChangeTracker.Clear();
        var approved = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.Equal(originalSubmission, approved!.SubmittedAtUtc);
        await fixture.Service.UpdateDescriptionAsync(forum.Id, "Approved edit", owner.Id);
        fixture.Context.ChangeTracker.Clear();
        var resubmitted = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.Equal(ForumStatus.Pending, resubmitted!.Status);
        Assert.True(resubmitted.SubmittedAtUtc >= originalSubmission);
        var resubmittedAt = resubmitted.SubmittedAtUtc;
        await fixture.Service.RejectAsync(forum.Id, "Reason");
        fixture.Context.ChangeTracker.Clear();
        var rejected = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.Equal(resubmittedAt, rejected!.SubmittedAtUtc);
        var timestamps = (rejected.CreatedAtUtc, rejected.SubmittedAtUtc, rejected.UpdatedAtUtc);
        await fixture.Service.UpdateDescriptionAsync(forum.Id, "Resubmitted after rejection", owner.Id);
        fixture.Context.ChangeTracker.Clear();
        var afterRejectEdit = await fixture.Context.Forums.FindAsync(forum.Id);
        Assert.True(afterRejectEdit!.SubmittedAtUtc >= timestamps.SubmittedAtUtc);
        Assert.Equal(timestamps.CreatedAtUtc, afterRejectEdit.CreatedAtUtc);
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
