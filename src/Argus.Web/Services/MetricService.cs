using Argus.Web.Data.Entities;
using Argus.Web.Repositories;

namespace Argus.Web.Services;

public class MetricService(IMetricRepository metrics) : IMetricService
{
    /// <summary>Group by Dimension or Target shows only the biggest few, so a chart stays readable (§4.3).</summary>
    private const int TopGroups = 5;

    public async Task<IReadOnlyList<ChartData>> GetDashboardAsync(int clientAppId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var charts = new List<ChartData>();
        foreach (var metric in await metrics.GetDefinitionsAsync(clientAppId, cancellationToken)) // a dozen charts: one small query each
            charts.Add(await GetChartAsync(metric, fromUtc, toUtc, cancellationToken));
        return charts;
    }

    public async Task<ChartData> GetChartAsync(MetricDefinition metric, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        // 1. Filter in SQL (client + name + time range).
        // shortcut: grouping and aggregating happen in C# after the filter; fine for a week of data, move them into SQL if ranges grow.
        var points = await metrics.GetPointsAsync(metric.ClientAppId, metric.Source, fromUtc, toUtc, cancellationToken);

        // 2. Every time bucket in the range, also the empty ones, so a quiet day shows as 0 instead of disappearing.
        var buckets = new List<DateTime>();
        for (var bucket = BucketStart(fromUtc, metric.Bucket); bucket < toUtc; bucket = NextBucket(bucket, metric.Bucket))
            buckets.Add(bucket);

        // 3. One series, or one per forum (Dimension) or post (Target), biggest first.
        IEnumerable<(string Name, List<DataPoint> Points)> groups = metric.GroupBy switch
        {
            MetricGroupBy.Dimension => points.GroupBy(point => point.Dimension ?? "(none)")
                .Select(group => (group.Key, group.ToList())),
            MetricGroupBy.Target => points.GroupBy(point => point.TargetId ?? "(none)")
                // A post's title can change: show the latest one.
                .Select(group => (group.MaxBy(point => point.TimestampUtc)!.TargetLabel ?? group.Key, group.ToList())),
            _ => [(metric.Name, points.ToList())],
        };
        if (metric.GroupBy != MetricGroupBy.None)
            groups = groups.OrderByDescending(group => Aggregate(metric.Aggregation, group.Points)).Take(TopGroups);

        // 4. Aggregate each series per bucket.
        var series = groups.Select(group =>
        {
            var byBucket = group.Points.ToLookup(point => BucketStart(point.TimestampUtc, metric.Bucket));
            return new ChartSeries(group.Name, buckets.Select(bucket => Aggregate(metric.Aggregation, byBucket[bucket].ToList())).ToList());
        }).ToList();

        var format = metric.Bucket is TimeBucket.Minute or TimeBucket.Hour ? "MM-dd HH:mm" : "yyyy-MM-dd";
        return new ChartData(metric, buckets.Select(bucket => bucket.ToString(format)).ToList(), series,
            Aggregate(metric.Aggregation, points.ToList()), points.Count > 0);
    }

    /// <summary>Count and Sum of nothing are 0; the others have no value, so the chart shows a gap.</summary>
    private static decimal? Aggregate(MetricAggregation aggregation, List<DataPoint> points) => aggregation switch
    {
        MetricAggregation.Count => points.Count,
        MetricAggregation.Sum => points.Sum(point => point.Value),
        _ when points.Count == 0 => null,
        MetricAggregation.Average => Math.Round(points.Average(point => point.Value), 2),
        MetricAggregation.Min => points.Min(point => point.Value),
        MetricAggregation.Max => points.Max(point => point.Value),
        MetricAggregation.Latest => points.MaxBy(point => point.TimestampUtc)!.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, null),
    };

    private static DateTime BucketStart(DateTime time, TimeBucket bucket) => bucket switch
    {
        TimeBucket.Minute => time.AddTicks(-(time.Ticks % TimeSpan.TicksPerMinute)),
        TimeBucket.Hour => time.AddTicks(-(time.Ticks % TimeSpan.TicksPerHour)),
        TimeBucket.Day => time.Date,
        _ => time.Date.AddDays(-(((int)time.DayOfWeek + 6) % 7)), // weeks start on Monday
    };

    private static DateTime NextBucket(DateTime start, TimeBucket bucket) => bucket switch
    {
        TimeBucket.Minute => start.AddMinutes(1),
        TimeBucket.Hour => start.AddHours(1),
        TimeBucket.Day => start.AddDays(1),
        _ => start.AddDays(7),
    };
}