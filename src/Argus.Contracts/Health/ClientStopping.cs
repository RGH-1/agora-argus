namespace Argus.Contracts.Health;

/// <summary>Client → Argus, routing key health.stopping. Sent on a clean shutdown, so Argus shows Stopped, not Down.</summary>
public class ClientStopping : ArgusMessage
{
    public string Reason { get; set; } = string.Empty;
}