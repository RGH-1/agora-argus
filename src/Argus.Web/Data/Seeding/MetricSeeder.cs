using Argus.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Data.Seeding;

/// <summary>
/// The default charts (§4.3). They are ordinary metric definitions, so the dashboard draws them like any other chart.
/// Runs on every start and only adds the charts a client doesn't have yet.
/// </summary>
public class MetricSeeder(ArgusDbContext db)
{
    public async Task SeedAsync()
    {
        var existing = await db.MetricDefinitions.Select(metric => new { metric.ClientAppId, metric.Name }).ToListAsync();

        foreach (var client in await db.ClientApps.ToListAsync())
        {
            // Matched by name: a restart adds nothing, and a default chart an operator changed keeps its settings.
            db.MetricDefinitions.AddRange(DefaultsFor(client)
                .Where(chart => !existing.Any(metric => metric.ClientAppId == client.Id && metric.Name == chart.Name)));
        }
        await db.SaveChangesAsync();
    }

    /// <summary>The charts every client starts with, plus Agora's analytics set.</summary>
    public static IEnumerable<MetricDefinition> DefaultsFor(ClientApp client)
    {
        // Every client: built on the heartbeat numbers, so these fill up as soon as the client runs.
        yield return Chart("Requests per minute", "sys.requests", MetricAggregation.Sum, TimeBucket.Minute);
        yield return Chart("Errors per minute", "sys.errors", MetricAggregation.Sum, TimeBucket.Minute, ChartType.Bar);
        yield return Chart("Average response time (ms)", "sys.avg_response_ms", MetricAggregation.Average, TimeBucket.Minute);

        if (client.ClientId != "agora") yield break;

        // Agora: built on the event names Agora sends (AA-44) and its reports.pending gauge.
        yield return Chart("New users per day", "user.registered", MetricAggregation.Count, TimeBucket.Day, ChartType.Bar);
        yield return Chart("Posts per day by forum", "Post.Added", MetricAggregation.Count, TimeBucket.Day, ChartType.Bar, MetricGroupBy.Dimension);
        yield return Chart("Reports per day", "content.reported", MetricAggregation.Count, TimeBucket.Day, ChartType.Bar);
        yield return Chart("Most-reported posts", "content.reported", MetricAggregation.Count, TimeBucket.Week, ChartType.Bar, MetricGroupBy.Target);
        yield return Chart("Pending reports", "reports.pending", MetricAggregation.Latest, TimeBucket.Minute, ChartType.NumberTile, alertAbove: 20);
        yield return Chart("Pro revenue per week", "subscription.purchased", MetricAggregation.Sum, TimeBucket.Week, ChartType.Bar);

        // A new object on every call: EF Core tracks each definition it adds.
        MetricDefinition Chart(string name, string source, MetricAggregation aggregation, TimeBucket bucket,
            ChartType chart = ChartType.Line, MetricGroupBy groupBy = MetricGroupBy.None, decimal? alertAbove = null) =>
            new()
            {
                ClientAppId = client.Id,
                Name = name,
                Source = source,
                Aggregation = aggregation,
                Bucket = bucket,
                GroupBy = groupBy,
                Chart = chart,
                AlertAbove = alertAbove,
            };
    }
}