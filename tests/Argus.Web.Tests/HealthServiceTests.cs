using Argus.Contracts;
using Argus.Contracts.Health;
using Argus.Web.Data;
using Argus.Web.Repositories;
using Argus.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Argus.Web.Tests;

public class HealthServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ArgusDbContext _db = null!;
    private HealthService _service = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new ArgusDbContext(new DbContextOptionsBuilder<ArgusDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync(); // seeds agora and demo
        _service = new HealthService(new ClientAppRepository(_db), new HealthRepository(_db), NullLogger<HealthService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Heartbeat_marks_the_client_healthy_and_stores_its_numbers()
    {
        await _service.ProcessAsync(new Heartbeat
        {
            ClientId = "agora",
            Requests = 12,
            Errors = 1,
            AvgResponseMs = 40,
            MemoryMb = 180,
            UptimeSec = 60,
            DbHealthy = true,
            DbResponseMs = 3,
            Gauges = new() { ["reports.pending"] = 4 },
        });

        var agora = await _db.ClientApps.SingleAsync(app => app.ClientId == "agora");
        Assert.Equal(HealthStatus.Healthy, agora.Health);
        Assert.NotNull(agora.LastHeartbeatAtUtc);

        var points = await _db.DataPoints.ToDictionaryAsync(point => point.Name, point => point.Value);
        Assert.Equal(12, points["sys.requests"]);
        Assert.Equal(1, points["sys.db_healthy"]);
        Assert.Equal(4, points["reports.pending"]);
        Assert.Equal(8, points.Count);
    }

    [Fact]
    public async Task Start_and_stop_are_recorded()
    {
        await _service.ProcessAsync(new ClientStarted { ClientId = "agora", Version = "1.2.0" });
        var agora = await _db.ClientApps.SingleAsync(app => app.ClientId == "agora");
        Assert.Equal((HealthStatus.Healthy, "1.2.0"), (agora.Health, agora.Version));
        Assert.NotNull(agora.LastHeartbeatAtUtc);

        await _service.ProcessAsync(new ClientStopping { ClientId = "agora" });
        Assert.Equal(HealthStatus.Stopped, agora.Health);
    }

    [Fact]
    public async Task Error_report_becomes_an_error_event()
    {
        await _service.ProcessAsync(new ErrorReported
        {
            ClientId = "agora",
            Message = new string('x', 3000),
            ExceptionType = "System.InvalidOperationException",
            Path = "/forums/3",
        });

        var error = await _db.ErrorEvents.SingleAsync();
        Assert.Equal(2000, error.Message.Length); // cut to the column's size
        Assert.Equal("/forums/3", error.Path);
    }

    [Theory]
    [InlineData("ghost")] // not registered
    [InlineData("demo")]  // suspended below
    public async Task Unknown_or_suspended_client_is_ignored(string clientId)
    {
        (await _db.ClientApps.SingleAsync(app => app.ClientId == "demo")).IsSuspended = true;
        await _db.SaveChangesAsync();

        await _service.ProcessAsync(new Heartbeat { ClientId = clientId });
        await _service.ProcessAsync(new ErrorReported { ClientId = clientId, Message = "boom", ExceptionType = "X" });

        Assert.Empty(await _db.DataPoints.ToListAsync());
        Assert.Empty(await _db.ErrorEvents.ToListAsync());
        Assert.Empty(await _db.ProcessedMessages.ToListAsync());
    }

    [Fact]
    public async Task The_same_message_twice_is_stored_once()
    {
        var error = new ErrorReported { ClientId = "agora", Message = "boom", ExceptionType = "X" };

        await _service.ProcessAsync(error);
        await _service.ProcessAsync(error); // RabbitMQ redelivered it

        Assert.Single(await _db.ErrorEvents.ToListAsync());
        Assert.Single(await _db.ProcessedMessages.ToListAsync());
    }


    [Theory]
    [InlineData(29, HealthStatus.Healthy)] // still within agora's 30 s timeout
    [InlineData(31, HealthStatus.Down)]    // silent too long: crashed or killed
    public async Task Watchdog_marks_a_silent_client_down(int secondsLater, HealthStatus expected)
    {
        await _service.ProcessAsync(new Heartbeat { ClientId = "agora" });
        var agora = await _db.ClientApps.SingleAsync(app => app.ClientId == "agora");

        await _service.MarkSilentClientsDownAsync(agora.LastHeartbeatAtUtc!.Value.AddSeconds(secondsLater));

        Assert.Equal(expected, agora.Health);
    }

    [Fact]
    public async Task Watchdog_leaves_stopped_and_never_seen_clients_alone()
    {
        await _service.ProcessAsync(new ClientStarted { ClientId = "agora", Version = "1.0.0" });
        await _service.ProcessAsync(new ClientStopping { ClientId = "agora" });

        await _service.MarkSilentClientsDownAsync(DateTime.UtcNow.AddHours(1));

        var health = await _db.ClientApps.ToDictionaryAsync(app => app.ClientId, app => app.Health);
        Assert.Equal(HealthStatus.Stopped, health["agora"]); // said goodbye, so no Down
        Assert.Equal(HealthStatus.Unknown, health["demo"]);  // never heard from
    }

    [Fact]
    public async Task A_heartbeat_after_down_makes_the_client_healthy_again()
    {
        await _service.ProcessAsync(new Heartbeat { ClientId = "agora" });
        await _service.MarkSilentClientsDownAsync(DateTime.UtcNow.AddMinutes(1));
        var agora = await _db.ClientApps.SingleAsync(app => app.ClientId == "agora");
        Assert.Equal(HealthStatus.Down, agora.Health);

        await _service.ProcessAsync(new Heartbeat { ClientId = "agora" });

        Assert.Equal(HealthStatus.Healthy, agora.Health);
    }
}