using Argus.Contracts;
using Argus.Contracts.Outbound;
using Argus.Web.Data;
using Argus.Web.Repositories;
using Argus.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Tests;

/// <summary>Runs the real ArgusDbContext on an in-memory SQLite database: fast, and no SQL Server needed.</summary>
public class ArgusDatabaseTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ArgusDbContext _db = null!;
    private ClientAppService _service = null!;
    private readonly FakeClientNotifier _notifier = new();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync(); // the in-memory database lives as long as this connection
        _db = new ArgusDbContext(new DbContextOptionsBuilder<ArgusDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync(); // creates the tables and inserts the seed data
        _service = new ClientAppService(new ClientAppRepository(_db), _notifier);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Seed_registers_agora_and_demo()
    {
        var apps = await _service.GetAllAsync();

        Assert.Equal(["agora", "demo"], apps.Select(app => app.ClientId).Order());
        Assert.All(apps, app =>
        {
            Assert.False(app.IsSuspended);
            Assert.Equal(HealthStatus.Unknown, app.Health);
            Assert.Equal(30, app.HeartbeatTimeoutSec);
        });
    }

    [Fact]
    public async Task Create_adds_a_client_app()
    {
        await _service.CreateAsync(" shop ", " My Shop ");

        var shop = await _service.GetByClientIdAsync("shop");
        Assert.NotNull(shop);
        Assert.Equal("My Shop", shop.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Agora")]     // uppercase
    [InlineData("my.shop")]   // '.' separates routing key words
    [InlineData("shop#")]     // '#' and '*' are routing key wildcards
    [InlineData("my shop")]
    public async Task Create_rejects_an_invalid_client_id(string clientId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(clientId, "Some app"));
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_client_id()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync("agora", "Another Agora"));
    }

    [Fact]
    public async Task Enums_are_stored_as_text()
    {
        var agora = (await _service.GetByClientIdAsync("agora"))!;
        agora.Health = HealthStatus.Down;
        await _db.SaveChangesAsync();

        var stored = await _db.Database.SqlQuery<string>($"SELECT Health AS Value FROM ClientApps WHERE ClientId = 'agora'").SingleAsync();
        Assert.Equal("Down", stored);
    }


    [Fact]
    public async Task Maintenance_mode_is_saved_and_sent_to_the_client()
    {
        Assert.True(await _service.SetMaintenanceAsync("agora", true, "  Back at 18:00  "));
        var agora = await _db.ClientApps.SingleAsync(app => app.ClientId == "agora");
        Assert.Equal((true, "Back at 18:00"), (agora.InMaintenance, agora.MaintenanceMessage));

        Assert.True(await _service.SetMaintenanceAsync("agora", false, null));
        Assert.Equal((false, null), (agora.InMaintenance, agora.MaintenanceMessage));

        var sent = _notifier.Sent.Select(item => (item.ClientId, ((MaintenanceModeChanged)item.Message).Enabled)).ToList();
        Assert.Equal([("agora", true), ("agora", false)], sent);
        Assert.False(await _service.SetMaintenanceAsync("ghost", true, null)); // unknown client
    }
}