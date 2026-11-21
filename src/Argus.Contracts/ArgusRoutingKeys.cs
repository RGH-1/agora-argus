namespace Argus.Contracts;

/// <summary>Routing keys for every message in the catalogue (design draft §6).</summary>
public static class ArgusRoutingKeys
{
    // Client → Argus, on argus.ingest
    public const string ClientStarted = "health.started";
    public const string ClientStopping = "health.stopping";
    public const string Heartbeat = "health.heartbeat";
    public const string ErrorReported = "health.error";
    public const string ActivityRecorded = "analytics.activity";

    // Binding patterns for Argus's queues
    public const string AllHealth = "health.*";
    public const string AllAnalytics = "analytics.*";

    // Argus → client, on argus.outbound: always prefixed with the ClientId
    public static string HealthStatusChanged(string clientId) => $"{clientId}.health.status";
    public static string MaintenanceModeChanged(string clientId) => $"{clientId}.health.maintenance";
    public static string InsightPublished(string clientId) => $"{clientId}.insights.published";

    /// <summary>Binding pattern for a client's inbox: everything addressed to that client.</summary>
    public static string AllForClient(string clientId) => $"{clientId}.#";
}