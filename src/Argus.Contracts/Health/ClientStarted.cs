namespace Argus.Contracts.Health;

/// <summary>Client → Argus, routing key health.started. Sent once when the client starts.</summary>
public class ClientStarted : ArgusMessage
{
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
}