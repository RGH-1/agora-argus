using System.Text.Json.Serialization;

namespace Argus.Contracts;

/// <summary>A client's health as Argus sees it. Sent as text ("Down"), so it reads well in the RabbitMQ UI.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HealthStatus>))]
public enum HealthStatus
{
    /// <summary>Argus has not heard from the client yet.</summary>
    Unknown = 0,
    Healthy,
    Degraded,
    /// <summary>Heartbeats stopped without a ClientStopping (crash, kill).</summary>
    Down,
    /// <summary>The client said goodbye with ClientStopping.</summary>
    Stopped
}