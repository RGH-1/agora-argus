using Argus.Contracts;

namespace Argus.Client;

/// <summary>The client's "Argus" configuration section (design draft §5.1).</summary>
public class ArgusOptions
{
    /// <summary>Who this client is in Argus, e.g. "agora". Must be registered as a client app in Argus.</summary>
    public string ClientId { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>Entity classes whose changes are sent automatically (stretch: EF Core interceptor).</summary>
    public string[] TrackEntities { get; set; } = [];

    /// <summary>The broker. The client never knows where Argus is, only where RabbitMQ is.</summary>
    public RabbitMqOptions RabbitMq { get; set; } = new();
}