namespace Argus.Contracts.Analytics;

/// <summary>
/// Client → Argus, routing key analytics.activity. One thing that happened in the client,
/// e.g. EventType "post.liked", Dimension "Crypto Deals", TargetId "42".
/// Argus stores it as a data point that the metric builder can chart.
/// </summary>
public class ActivityRecorded : ArgusMessage
{
    public string EventType { get; set; } = string.Empty;

    /// <summary>1 for a plain count; an amount for sums, e.g. 4.99 for a payment.</summary>
    public decimal Value { get; set; } = 1;

    /// <summary>What to group by, e.g. the forum name.</summary>
    public string? Dimension { get; set; }

    /// <summary>The item it happened to, e.g. a post's id, for "top 5" charts.</summary>
    public string? TargetId { get; set; }
    public string? TargetLabel { get; set; }

    /// <summary>Who did it (the client's own user id), if known.</summary>
    public string? ActorId { get; set; }
}