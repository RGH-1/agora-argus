using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Argus.Web.Data.Seeding;
using Argus.Web.Repositories;
using Argus.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Tests;

/// <summary>The generic metric query on the in-memory SQLite database: every aggregation, bucket and group-by.</summary>
public class MetricServiceTests : IAsyncLifetime
{
    private const int Agora = 1, Demo = 2; // the seeded client apps
    private static readonly DateTime Friday = new(2026, 11, 20);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ArgusDbContext _db = null!;
    private MetricService _service = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new ArgusDbContext(new DbContextOptionsBuilder<ArgusDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync(); // seeds agora and demo
        _service = new MetricService(new MetricRepository(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task AddAsync(params DataPoint[] points)
    {
        _db.DataPoints.AddRange(points);
        await _db.SaveChangesAsync();
    }

    private static DataPoint Point(DateTime at, decimal value = 1, string name = "post.liked", int client = Agora,
        string? dimension = null, string? targetId = null, string? targetLabel = null) =>
        new() { ClientAppId = client, Name = name, Value = value, TimestampUtc = at, Dimension = dimension, TargetId = targetId, TargetLabel = targetLabel };

    private static MetricDefinition Metric(MetricAggregation aggregation, TimeBucket bucket, MetricGroupBy groupBy = MetricGroupBy.None) =>
        new() { ClientAppId = Agora, Name = "Likes", Source = "post.liked", Aggregation = aggregation, Bucket = bucket, GroupBy = groupBy };

    [Fact]
    public async Task Counts_per_day_only_this_clients_points_in_the_range_and_shows_empty_days_as_zero()
    {
        await AddAsync(
            Point(Friday.AddHours(9)), Point(Friday.AddHours(18)), Point(Friday.AddDays(2).AddHours(1)),
            Point(Friday.AddDays(-1)),                       // before the range
            Point(Friday.AddDays(3)),                        // the range ends at midnight, excluded
            Point(Friday.AddHours(9), client: Demo),         // another client
            Point(Friday.AddHours(9), name: "post.unliked")); // another name

        var chart = await _service.GetChartAsync(Metric(MetricAggregation.Count, TimeBucket.Day), Friday, Friday.AddDays(3));

        Assert.Equal(["2026-11-20", "2026-11-21", "2026-11-22"], chart.Labels);
        Assert.Equal([2m, 0m, 1m], Assert.Single(chart.Series).Values);
        Assert.Equal("Likes", chart.Series[0].Name);
        Assert.Equal(3, chart.Total);
        Assert.True(chart.HasData);
    }

    public static TheoryData<MetricAggregation, decimal?[], decimal?> Aggregations => new()
    {
        { MetricAggregation.Count, [2, 0, 1], 3 },
        { MetricAggregation.Sum, [6, 0, 7], 13 },
        { MetricAggregation.Average, [3, null, 7], 4.33m }, // no value is a gap, not 0
        { MetricAggregation.Min, [2, null, 7], 2 },
        { MetricAggregation.Max, [4, null, 7], 7 },
        { MetricAggregation.Latest, [2, null, 7], 7 },
    };

    [Theory]
    [MemberData(nameof(Aggregations))]
    public async Task Every_aggregation_works_per_hour(MetricAggregation aggregation, decimal?[] values, decimal? total)
    {
        var ten = Friday.AddHours(10);
        await AddAsync(Point(ten.AddMinutes(5), 4), Point(ten.AddMinutes(50), 2), Point(ten.AddHours(2).AddMinutes(10), 7));

        var chart = await _service.GetChartAsync(Metric(aggregation, TimeBucket.Hour), ten, ten.AddHours(3));

        Assert.Equal(["11-20 10:00", "11-20 11:00", "11-20 12:00"], chart.Labels);
        Assert.Equal(values, chart.Series[0].Values);
        Assert.Equal(total, chart.Total);
    }

    [Fact]
    public async Task Minute_and_week_buckets()
    {
        await AddAsync(Point(Friday.AddHours(10).AddSeconds(30)), Point(Friday.AddHours(10).AddMinutes(2)));

        var minutes = await _service.GetChartAsync(Metric(MetricAggregation.Count, TimeBucket.Minute), Friday.AddHours(10), Friday.AddHours(10).AddMinutes(3));
        Assert.Equal(["11-20 10:00", "11-20 10:01", "11-20 10:02"], minutes.Labels);
        Assert.Equal([1m, 0m, 1m], minutes.Series[0].Values);

        // Weeks start on Monday: Friday the 20th is in the week of the 16th.
        var weeks = await _service.GetChartAsync(Metric(MetricAggregation.Count, TimeBucket.Week), Friday.AddDays(-2), Friday.AddDays(12));
        Assert.Equal(["2026-11-16", "2026-11-23", "2026-11-30"], weeks.Labels);
        Assert.Equal([2m, 0m, 0m], weeks.Series[0].Values);
    }

    [Fact]
    public async Task Group_by_dimension_shows_the_five_biggest_first()
    {
        // Forum "f6" gets 6 likes, ..., forum "f1" gets 1: f1 is the one left out.
        await AddAsync(Enumerable.Range(1, 6).SelectMany(forum =>
            Enumerable.Range(0, forum).Select(_ => Point(Friday.AddHours(9), dimension: $"f{forum}"))).ToArray());

        var chart = await _service.GetChartAsync(Metric(MetricAggregation.Count, TimeBucket.Day, MetricGroupBy.Dimension), Friday, Friday.AddDays(1));

        Assert.Equal(["f6", "f5", "f4", "f3", "f2"], chart.Series.Select(series => series.Name));
        Assert.Equal([6m], chart.Series[0].Values);
    }

    [Fact]
    public async Task Group_by_target_names_each_post_by_its_latest_title()
    {
        await AddAsync(
            Point(Friday.AddHours(9), targetId: "7", targetLabel: "Old title"),
            Point(Friday.AddHours(10), targetId: "7", targetLabel: "New title"),
            Point(Friday.AddHours(11), targetId: "8", targetLabel: "Other post"));

        var chart = await _service.GetChartAsync(Metric(MetricAggregation.Count, TimeBucket.Day, MetricGroupBy.Target), Friday, Friday.AddDays(1));

        Assert.Equal([("New title", 2m), ("Other post", 1m)], chart.Series.Select(series => (series.Name, series.Values[0]!.Value)));
    }

    [Fact]
    public async Task Dashboard_draws_every_chart_of_the_client_including_number_tiles()
    {
        await new MetricSeeder(_db).SeedAsync();
        await AddAsync(Point(Friday.AddMinutes(1), 3, "reports.pending"), Point(Friday.AddMinutes(2), 5, "reports.pending"));

        var charts = await _service.GetDashboardAsync(Agora, Friday, Friday.AddHours(1));

        Assert.Equal(9, charts.Count); // Agora's 3 sys.* charts and its 6 analytics charts
        var pending = Assert.Single(charts, chart => chart.Metric.Chart == ChartType.NumberTile);
        Assert.Equal(5, pending.Total); // Latest
        Assert.False(charts.Single(chart => chart.Metric.Name == "New users per day").HasData);
    }
}