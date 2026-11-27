using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Controllers;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Argus.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Agora.Web.Tests;

public sealed class AgoraSubscriptionTests
{
    [Fact]
    public async Task Free_member_upgrade_persists_pro_status_and_tracks_the_exact_purchase_event()
    {
        await using var fixture = await SubscriptionFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("free-member");

        var result = await fixture.Service.UpgradeToProAsync(user.Id);

        Assert.True(result.Succeeded);
        fixture.Context.ChangeTracker.Clear();
        Assert.True((await fixture.Context.Users.FindAsync(user.Id))!.IsPro);
        var activity = Assert.Single(fixture.Tracker.Activities);
        Assert.Equal("subscription.purchased", activity.EventType);
        Assert.Equal(4.99m, activity.Value);
        Assert.Equal(user.Id, activity.ActorId);
    }

    [Fact]
    public async Task Repeated_upgrade_is_already_pro_and_does_not_emit_another_purchase()
    {
        await using var fixture = await SubscriptionFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("repeat");

        Assert.True((await fixture.Service.UpgradeToProAsync(user.Id)).Succeeded);
        var repeat = await fixture.Service.UpgradeToProAsync(user.Id);

        Assert.Equal(SubscriptionUpgradeOutcome.AlreadyPro, repeat.Outcome);
        Assert.Single(fixture.Tracker.Activities);
    }

    [Fact]
    public async Task Banned_or_existing_pro_users_are_not_upgraded_or_tracked()
    {
        await using var fixture = await SubscriptionFixture.CreateAsync();
        var banned = await fixture.CreateUserAsync("banned", isBanned: true);
        var pro = await fixture.CreateUserAsync("pro", isPro: true);

        Assert.Equal(SubscriptionUpgradeOutcome.Banned, (await fixture.Service.UpgradeToProAsync(banned.Id)).Outcome);
        Assert.Equal(SubscriptionUpgradeOutcome.AlreadyPro, (await fixture.Service.UpgradeToProAsync(pro.Id)).Outcome);
        Assert.Empty(fixture.Tracker.Activities);
    }

    [Fact]
    public async Task Tracker_failure_does_not_roll_back_a_completed_membership_upgrade()
    {
        await using var fixture = await SubscriptionFixture.CreateAsync();
        fixture.Tracker.Exception = new InvalidOperationException("broker unavailable");
        var user = await fixture.CreateUserAsync("resilient");

        Assert.True((await fixture.Service.UpgradeToProAsync(user.Id)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        Assert.True((await fixture.Context.Users.FindAsync(user.Id))!.IsPro);
    }

    [Fact]
    public async Task Atomic_transition_allows_only_one_successful_purchase_for_repeated_requests()
    {
        await using var fixture = await SubscriptionFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("atomic");

        var first = await fixture.Service.UpgradeToProAsync(user.Id);
        var second = await fixture.Service.UpgradeToProAsync(user.Id);

        Assert.True(first.Succeeded);
        Assert.Equal(SubscriptionUpgradeOutcome.AlreadyPro, second.Outcome);
        Assert.Single(fixture.Tracker.Activities);
    }

    [Fact]
    public async Task Upgraded_member_can_create_forums_beyond_the_existing_free_limit_and_they_remain_pending()
    {
        await using var fixture = await SubscriptionFixture.CreateAsync();
        var user = await fixture.CreateUserAsync("forum-owner");
        var forums = new ForumService(new ForumRepository(fixture.Context));
        for (var number = 1; number <= 3; number++)
            await forums.CreateAsync($"Forum {number}", "Description", user.Id);

        await Assert.ThrowsAsync<ForumBusinessRuleException>(() => forums.CreateAsync("Forum 4", "Description", user.Id));
        Assert.True((await fixture.Service.UpgradeToProAsync(user.Id)).Succeeded);
        var fourth = await forums.CreateAsync("Forum 4", "Description", user.Id);
        var fifth = await forums.CreateAsync("Forum 5", "Description", user.Id);

        Assert.Equal(ForumStatus.Pending, fourth.Status);
        Assert.Equal(ForumStatus.Pending, fifth.Status);
    }

    [Fact]
    public async Task Initial_successful_checkout_result_still_shows_the_welcome_message()
    {
        await using var fixture = await SubscriptionResultFixture.CreateAsync();
        var controller = fixture.CreateController();
        controller.TempData["SubscriptionOutcome"] = nameof(SubscriptionUpgradeOutcome.Succeeded);

        var result = Assert.IsType<ViewResult>(await controller.Result());
        var model = Assert.IsType<Agora.Web.Models.Subscription.SubscriptionResultViewModel>(result.Model);

        Assert.True(model.Succeeded);
        Assert.False(model.AlreadyPro);
    }

    [Fact]
    public async Task Refreshing_result_for_an_active_pro_member_shows_active_membership_without_checkout()
    {
        await using var fixture = await SubscriptionResultFixture.CreateAsync();
        var user = await fixture.CreateUserAsync(isPro: true);
        var controller = fixture.CreateController(user.Id);

        var result = Assert.IsType<ViewResult>(await controller.Result());
        var model = Assert.IsType<Agora.Web.Models.Subscription.SubscriptionResultViewModel>(result.Model);

        Assert.False(model.Succeeded);
        Assert.True(model.AlreadyPro);
        Assert.Equal("Your Agora Pro membership is already active.", model.Message);
    }

    [Fact]
    public async Task Gateway_get_is_read_only_and_exposes_free_pro_and_banned_states()
    {
        await using var fixture = await SubscriptionResultFixture.CreateAsync();
        var free = await fixture.CreateUserAsync();
        var pro = await fixture.CreateUserAsync("pro-result", isPro: true);
        var banned = await fixture.CreateUserAsync("banned-result", isBanned: true);

        var freeModel = Assert.IsType<Agora.Web.Models.Subscription.SubscriptionUpgradeViewModel>((Assert.IsType<ViewResult>(await fixture.CreateController(free.Id).Checkout())).Model);
        var proModel = Assert.IsType<Agora.Web.Models.Subscription.SubscriptionUpgradeViewModel>((Assert.IsType<ViewResult>(await fixture.CreateController(pro.Id).Checkout())).Model);
        var bannedModel = Assert.IsType<Agora.Web.Models.Subscription.SubscriptionUpgradeViewModel>((Assert.IsType<ViewResult>(await fixture.CreateController(banned.Id).Checkout())).Model);

        Assert.False(freeModel.IsPro); Assert.False(freeModel.IsBanned);
        Assert.True(proModel.IsPro);
        Assert.True(bannedModel.IsBanned);
    }

    [Fact]
    public async Task Checkout_post_redirects_to_one_time_receipt_without_accepting_browser_payment_data()
    {
        await using var fixture = await SubscriptionResultFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var controller = fixture.CreateController(user.Id, new FixedSubscriptionService(SubscriptionUpgradeOutcome.Succeeded));

        var result = Assert.IsType<RedirectToActionResult>(await controller.ConfirmCheckout());

        Assert.Equal(nameof(SubscriptionController.Result), result.ActionName);
        Assert.Equal(nameof(SubscriptionUpgradeOutcome.Succeeded), controller.TempData["SubscriptionOutcome"]);
        Assert.NotNull(controller.TempData["SubscriptionReceiptTimeUtc"]);
        Assert.NotNull(controller.TempData["SubscriptionReceiptReference"]);
    }

    private sealed class SubscriptionFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly AgoraDbContext context;
        private readonly SubscriptionRepository repository;
        private SubscriptionFixture(SqliteConnection connection, AgoraDbContext context)
        {
            this.connection = connection; this.context = context; repository = new SubscriptionRepository(context);
            Tracker = new FakeTracker(); Service = new SubscriptionService(repository, Tracker, NullLogger<SubscriptionService>.Instance);
        }
        public AgoraDbContext Context => context;
        public FakeTracker Tracker { get; }
        public ISubscriptionService Service { get; }
        public static async Task<SubscriptionFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var context = new AgoraDbContext(new DbContextOptionsBuilder<AgoraDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync(); return new SubscriptionFixture(connection, context);
        }
        public async Task<ApplicationUser> CreateUserAsync(string name, bool isPro = false, bool isBanned = false)
        {
            var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name, IsPro = isPro, IsBanned = isBanned };
            Context.Users.Add(user); await Context.SaveChangesAsync(); return user;
        }
        public async ValueTask DisposeAsync() { await context.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class FakeTracker : IArgusTracker
    {
        public List<Activity> Activities { get; } = [];
        public Exception? Exception { get; set; }
        public Task TrackAsync(string eventType, decimal value = 1, string? dimension = null, string? targetId = null, string? targetLabel = null, string? actorId = null)
        {
            if (Exception is not null) return Task.FromException(Exception);
            Activities.Add(new Activity(eventType, value, actorId)); return Task.CompletedTask;
        }
    }
    private sealed record Activity(string EventType, decimal Value, string? ActorId);

    private sealed class SubscriptionResultFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider provider;
        private SubscriptionResultFixture(SqliteConnection connection, ServiceProvider provider)
        {
            this.connection = connection; this.provider = provider;
            Context = provider.GetRequiredService<AgoraDbContext>();
            UserManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        }
        public AgoraDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public static async Task<SubscriptionResultFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection(); services.AddLogging();
            services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<AgoraDbContext>();
            var provider = services.BuildServiceProvider();
            var fixture = new SubscriptionResultFixture(connection, provider);
            await fixture.Context.Database.EnsureCreatedAsync(); return fixture;
        }
        public async Task<ApplicationUser> CreateUserAsync(string userName = "result-user", bool isPro = false, bool isBanned = false)
        {
            var user = new ApplicationUser { UserName = userName, Email = $"{userName}@agora.local", DisplayName = "Result user", IsPro = isPro, IsBanned = isBanned };
            Context.Users.Add(user); await Context.SaveChangesAsync(); return user;
        }
        public SubscriptionController CreateController(string? userId = null, ISubscriptionService? subscriptionService = null)
        {
            var controller = new SubscriptionController(UserManager, subscriptionService ?? new ThrowingSubscriptionService())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.TempData = new TempDataDictionary(controller.HttpContext, new InMemoryTempDataProvider());
            if (userId is not null)
                controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
            return controller;
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class ThrowingSubscriptionService : ISubscriptionService
    {
        public Task<SubscriptionUpgradeResult> UpgradeToProAsync(string userId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A GET request must not run checkout.");
    }

    private sealed class FixedSubscriptionService(SubscriptionUpgradeOutcome outcome) : ISubscriptionService
    {
        public Task<SubscriptionUpgradeResult> UpgradeToProAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubscriptionUpgradeResult(outcome));
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
