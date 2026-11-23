using System.ComponentModel.DataAnnotations;

namespace Argus.Web.Data.Entities;

/// <summary>A chart an operator built in the metric builder (§4.3).</summary>
public class MetricDefinition
{
    public int Id { get; set; }
    public int ClientAppId { get; set; }
    public ClientApp ClientApp { get; set; } = null!;
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>The DataPoint name the chart is built from, e.g. "post.liked".</summary>
    [Required, StringLength(100)] public string Source { get; set; } = string.Empty;
    public MetricAggregation Aggregation { get; set; } = MetricAggregation.Count;
    public TimeBucket Bucket { get; set; } = TimeBucket.Day;
    public MetricGroupBy GroupBy { get; set; } = MetricGroupBy.None;
    public ChartType Chart { get; set; } = ChartType.Line;

    /// <summary>Optional alert: if the latest value is above this, the client becomes Degraded.</summary>
    public decimal? AlertAbove { get; set; }
}