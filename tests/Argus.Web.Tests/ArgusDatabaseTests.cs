using Argus.Contracts;
using Argus.Contracts.Outbound;
using Argus.Web.Data;
using Argus.Web.Data.Entities;
using System.ComponentModel.DataAnnotations;
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

    [Fact]
    public async Task Update_saves_name_suspension_and_thresholds()
    {
        var settings = new ClientAppSettings { Name = " Agora forum ", IsSuspended = true, HeartbeatTimeoutSec = 60, SlowDbMs = 200, MaxErrorRatePercent = 10 };

        Assert.True(await _service.UpdateAsync("agora", settings));

        var agora = await _db.ClientApps.AsNoTracking().SingleAsync(app => app.ClientId == "agora");
        Assert.Equal(("Agora forum", true, 60, 200, 10), (agora.Name, agora.IsSuspended, agora.HeartbeatTimeoutSec, agora.SlowDbMs, agora.MaxErrorRatePercent));
        Assert.False(await _service.UpdateAsync("ghost", settings)); // unknown client
    }

    [Theory]
    [InlineData(10, 500, 5)]  // timeout shorter than the 10 s heartbeat interval allows
    [InlineData(30, 0, 5)]    // slow database must be at least 1 ms
    [InlineData(30, 500, 101)] // error rate is a percentage
    public async Task Update_rejects_thresholds_out_of_range(int timeoutSec, int slowDbMs, int maxErrorRate)
    {
        var settings = new ClientAppSettings { Name = "Agora", HeartbeatTimeoutSec = timeoutSec, SlowDbMs = slowDbMs, MaxErrorRatePercent = maxErrorRate };

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateAsync("agora", settings));
    }

    [Fact]
    public async Task Delete_removes_the_client_and_all_its_data()
    {
        var shop = await _service.CreateAsync("shop", "Shop");
        _db.DataPoints.Add(new DataPoint { ClientAppId = shop.Id, Name = "order.placed", Value = 1, TimestampUtc = DateTime.UtcNow });
        _db.Incidents.Add(new Incident { ClientAppId = shop.Id, Cause = HealthStatus.Down, Reason = "No heartbeat", OpenedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear(); // like a new request: the rows are only in the database

        Assert.True(await _service.DeleteAsync("shop"));

        Assert.Null(await _service.GetByClientIdAsync("shop"));
        Assert.Equal((0, 0), (await _db.DataPoints.CountAsync(), await _db.Incidents.CountAsync())); // removed by the cascade
        Assert.False(await _service.DeleteAsync("shop"));
    }
}