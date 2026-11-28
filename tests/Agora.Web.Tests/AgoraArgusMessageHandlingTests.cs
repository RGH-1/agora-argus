using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Services;
using Argus.Client;
using Argus.Contracts;
using Argus.Contracts.Outbound;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Agora.Web.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agora.Web.Tests;

public sealed class AgoraArgusMessageHandlingTests
{
    [Fact]
    public void Argus_panel_is_limited_to_staff_and_public_hub_exposes_no_state_changing_methods()
    {
        var controller = typeof(Agora.Web.Areas.Moderation.Controllers.ArgusController);
        var authorize = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Moderator,Admin", authorize.Roles);
        Assert.DoesNotContain(typeof(MaintenanceStatusHub).GetMethods(), method =>
            method.DeclaringType == typeof(MaintenanceStatusHub) && method.Name != nameof(MaintenanceStatusHub.OnConnectedAsync));
    }

    [Fact]
    public async Task Maintenance_events_persist_deduplicate_and_do_not_allow_stale_events_to_win()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newer = DateTimeOffset.UtcNow;
        await fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = newer, Enabled = false });
        var state = await fixture.State.GetAsync();
        Assert.False(state.Enabled);

        await fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = newer.AddMinutes(-1), Enabled = true, Message = "old" });
        Assert.False((await fixture.State.GetAsync()).Enabled);
        Assert.Single(fixture.Notifier.Statuses);

        var duplicate = new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = newer.AddMinutes(1), Enabled = true, Message = "Planned work" };
        await fixture.Handler.HandleMaintenanceModeChangedAsync(duplicate);
        await fixture.Handler.HandleMaintenanceModeChangedAsync(duplicate);
        var persisted = await fixture.Context.MaintenanceStates.SingleAsync();
        Assert.True(persisted.IsEnabled);
        Assert.Equal("Planned work", persisted.Message);
        Assert.Equal(2, fixture.Notifier.Statuses.Count);
    }

    [Fact]
    public async Task Maintenance_rejects_wrong_client_and_default_message_is_safe()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "other", Enabled = true }));
        Assert.Empty(await fixture.Context.ProcessedArgusMessages.ToListAsync());

        await fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = DateTimeOffset.UtcNow, Enabled = true });
        Assert.Equal("Agora is currently under maintenance. Some actions are temporarily unavailable.", (await fixture.State.GetAsync()).DisplayMessage);
    }

    [Fact]
    public async Task Maintenance_state_is_visible_to_a_new_context_and_off_restores_downstream_writes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = now, Enabled = true });
        await using var secondContext = new AgoraDbContext(fixture.Options);
        var secondHandler = new AgoraArgusMessageHandler(secondContext, new ArgusOptions { ClientId = "agora" }, fixture.Notifier, NullLogger<AgoraArgusMessageHandler>.Instance);
        await secondHandler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = now.AddMinutes(1), Enabled = false });
        Assert.False((await new MaintenanceStateService(fixture.Context).GetAsync()).Enabled);
    }

    [Fact]
    public async Task Health_and_insight_messages_are_persisted_once_and_panel_is_newest_first()
    {
        await using var fixture = await Fixture.CreateAsync();
        var older = DateTimeOffset.UtcNow.AddMinutes(-2);
        var newest = DateTimeOffset.UtcNow;
        var health = new HealthStatusChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = older, OldStatus = HealthStatus.Healthy, NewStatus = HealthStatus.Degraded, Reason = "Load", IncidentId = 7 };
        await fixture.Handler.HandleHealthStatusChangedAsync(health);
        await fixture.Handler.HandleHealthStatusChangedAsync(health);
        await fixture.Handler.HandleHealthStatusChangedAsync(new HealthStatusChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = newest, OldStatus = HealthStatus.Degraded, NewStatus = HealthStatus.Healthy, Reason = "Recovered" });
        await fixture.Handler.HandleInsightPublishedAsync(new InsightPublished { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = newest, Title = "Review capacity", Detail = "Scale before peak traffic.", Severity = InsightSeverity.Warning });

        var panel = await fixture.Panel.GetAsync();
        Assert.Equal(2, panel.HealthEvents.Count);
        Assert.Equal(HealthStatus.Healthy, panel.LatestHealth!.NewStatus);
        Assert.Single(panel.Insights);
        Assert.Equal("Review capacity", panel.Insights[0].Title);
    }

    [Fact]
    public async Task Maintenance_middleware_blocks_writes_but_allows_reads_and_login()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = DateTimeOffset.UtcNow, Enabled = true });
        var called = false;
        var middleware = new Agora.Web.Middleware.MaintenanceModeMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<Agora.Web.Middleware.MaintenanceModeMiddleware>.Instance);
        var write = new DefaultHttpContext(); write.Request.Method = HttpMethods.Post; write.Request.Path = "/Forums/Create";
        await middleware.InvokeAsync(write, fixture.State);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, write.Response.StatusCode); Assert.False(called);
        var read = new DefaultHttpContext(); read.Request.Method = HttpMethods.Get; read.Request.Path = "/Forums";
        await middleware.InvokeAsync(read, fixture.State);
        Assert.True(called);
        called = false;
        var login = new DefaultHttpContext(); login.Request.Method = HttpMethods.Post; login.Request.Path = "/Account/Login";
        await middleware.InvokeAsync(login, fixture.State);
        Assert.True(called);
    }

    [Fact]
    public async Task Missing_singleton_fails_closed_and_handler_does_not_attempt_identity_insert()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.MaintenanceStates.RemoveRange(fixture.Context.MaintenanceStates);
        await fixture.Context.SaveChangesAsync();

        var status = await fixture.State.GetAsync();
        Assert.True(status.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged
        {
            MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = DateTimeOffset.UtcNow, Enabled = true
        }));
        Assert.Empty(await fixture.Context.ProcessedArgusMessages.ToListAsync());
    }

    [Fact]
    public async Task Maintenance_http_pipeline_returns_503_without_reaching_business_endpoint_and_allows_required_exceptions()
    {
        var writes = 0;
        await using var app = await CreateMaintenanceTestAppAsync(() => writes++);
        var client = app.GetTestClient();

        var blocked = await client.PostAsync("/Forums/Create", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, (int)blocked.StatusCode);
        Assert.Equal(0, writes);
        var json = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("maintenance", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<strong>", json, StringComparison.OrdinalIgnoreCase);

        Assert.True((await client.GetAsync("/Forums")).IsSuccessStatusCode);
        Assert.True((await client.PostAsync("/Account/Login", new StringContent(string.Empty))).IsSuccessStatusCode);
        Assert.True((await client.PostAsync("/hubs/maintenance/negotiate", new StringContent(string.Empty))).IsSuccessStatusCode);
    }

    private static async Task<WebApplication> CreateMaintenanceTestAppAsync(Action onBusinessWrite)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IMaintenanceStateService>(new FixedMaintenanceStateService(new MaintenanceStatus(true, "<strong>Encoded</strong>", DateTimeOffset.UtcNow)));
        var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<Agora.Web.Middleware.MaintenanceModeMiddleware>();
        app.MapGet("/Forums", () => Results.Ok());
        app.MapPost("/Forums/Create", () => { onBusinessWrite(); return Results.Ok(); });
        app.MapPost("/Account/Login", () => Results.Ok());
        app.MapPost("/hubs/maintenance/negotiate", () => Results.Ok());
        await app.StartAsync();
        return app;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private Fixture(SqliteConnection connection, AgoraDbContext context, SpyNotifier notifier)
        {
            _connection = connection; Context = context; Notifier = notifier;
            State = new MaintenanceStateService(context);
            Handler = new AgoraArgusMessageHandler(context, new ArgusOptions { ClientId = "agora" }, notifier, NullLogger<AgoraArgusMessageHandler>.Instance);
            Panel = new ArgusPanelService(context, State);
        }
        public AgoraDbContext Context { get; }
        public DbContextOptions<AgoraDbContext> Options => Context.Database.GetDbConnection() is { } connection
            ? new DbContextOptionsBuilder<AgoraDbContext>().UseSqlite((SqliteConnection)connection).Options
            : throw new InvalidOperationException();
        public SpyNotifier Notifier { get; }
        public MaintenanceStateService State { get; }
        public AgoraArgusMessageHandler Handler { get; }
        public ArgusPanelService Panel { get; }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var context = new AgoraDbContext(new DbContextOptionsBuilder<AgoraDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            context.MaintenanceStates.Add(new MaintenanceState { IsEnabled = false, UpdatedAtUtc = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
            return new Fixture(connection, context, new SpyNotifier());
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class SpyNotifier : IPublicMaintenanceNotifier
    {
        public List<MaintenanceStatus> Statuses { get; } = [];
        public Task StatusChangedAsync(MaintenanceStatus status) { Statuses.Add(status); return Task.CompletedTask; }
    }

    private sealed class FixedMaintenanceStateService(MaintenanceStatus status) : IMaintenanceStateService
    {
        public Task<MaintenanceStatus> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(status);
    }
}
