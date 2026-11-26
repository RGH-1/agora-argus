using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Argus.Web.Data.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Tests;

public class MetricSeederTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ArgusDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new ArgusDbContext(new DbContextOptionsBuilder<ArgusDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync(); // seeds agora and demo
        await new MetricSeeder(_db).SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private Task<List<string>> ChartsOfAsync(string clientId) =>
        _db.MetricDefinitions.Where(metric => metric.ClientApp.ClientId == clientId)
            .OrderBy(metric => metric.Id).Select(metric => metric.Name).ToListAsync();

    [Fact]
    public async Task Every_client_gets_the_sys_charts_and_agora_also_gets_its_analytics()
    {
        string[] system = ["Requests per minute", "Errors per minute", "Average response time (ms)"];

        Assert.Equal(system, await ChartsOfAsync("demo"));
        Assert.Equal([.. system, "New users per day", "Posts per day by forum", "Reports per day",
            "Most-reported posts", "Pending reports", "Pro revenue per week"], await ChartsOfAsync("agora"));

        var pending = await _db.MetricDefinitions.SingleAsync(metric => metric.Name == "Pending reports");
        Assert.Equal(("reports.pending", MetricAggregation.Latest, ChartType.NumberTile, 20m),
            (pending.Source, pending.Aggregation, pending.Chart, pending.AlertAbove));
    }

    [Fact]
    public async Task A_restart_adds_nothing_and_keeps_an_operators_changes()
    {
        var requests = await _db.MetricDefinitions.FirstAsync(metric => metric.Name == "Requests per minute");
        requests.Bucket = TimeBucket.Hour; // an operator changed it
        await _db.SaveChangesAsync();

        await new MetricSeeder(_db).SeedAsync(); // like the next app start

        Assert.Equal(12, await _db.MetricDefinitions.CountAsync());
        Assert.Equal(TimeBucket.Hour, (await _db.MetricDefinitions.FindAsync(requests.Id))!.Bucket);
    }

    [Fact]
    public async Task A_client_added_later_gets_the_sys_charts_on_the_next_start()
    {
        _db.ClientApps.Add(new ClientApp { ClientId = "shop", Name = "Shop" });
        await _db.SaveChangesAsync();

        await new MetricSeeder(_db).SeedAsync();

        Assert.Equal(3, (await ChartsOfAsync("shop")).Count);
    }
}