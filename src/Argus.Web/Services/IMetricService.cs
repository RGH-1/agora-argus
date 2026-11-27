using Argus.Web.Data.Entities;

namespace Argus.Web.Services;

/// <summary>One line or bar set: the metric itself, or one forum or post when the chart is grouped.</summary>
public record ChartSeries(string Name, IReadOnlyList<decimal?> Values);

/// <summary>
/// A chart ready to draw: one label per time bucket and one value per label in each series.
/// Total is the aggregation over the whole range (what a number tile shows).
/// </summary>
public record ChartData(MetricDefinition Metric, IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series, decimal? Total, bool HasData);

public interface IMetricService
{
    /// <summary>Every chart of the client, for the dashboard.</summary>
    Task<IReadOnlyList<ChartData>> GetDashboardAsync(int clientAppId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>The one generic query (§4.3): a metric definition and a time range in, labels and values out.</summary>
    Task<ChartData> GetChartAsync(MetricDefinition metric, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}