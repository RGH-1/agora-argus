namespace Argus.Contracts.Health;

/// <summary>
/// Client → Argus, routing key health.heartbeat. Sent every HeartbeatIntervalSeconds.
/// Requests, Errors and AvgResponseMs cover only the interval since the previous heartbeat.
/// </summary>
public class Heartbeat : ArgusMessage
{
    /// <summary>null when the client has no database.</summary>
    public bool? DbHealthy { get; set; }

    /// <summary>How long the database check took; null when the client has no database.</summary>
    public double? DbResponseMs { get; set; }

    public double MemoryMb { get; set; }
    public long UptimeSec { get; set; }
    public int Requests { get; set; }
    public int Errors { get; set; }
    public double AvgResponseMs { get; set; }

    /// <summary>Client-defined values read on every heartbeat, e.g. "reports.pending" → 4.</summary>
    public Dictionary<string, decimal> Gauges { get; set; } = new();
}