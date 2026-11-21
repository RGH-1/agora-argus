namespace Argus.Contracts.Outbound;

/// <summary>Argus → client, routing key {clientId}.health.maintenance. An operator switched maintenance mode.</summary>
public class MaintenanceModeChanged : ArgusMessage
{
    public bool Enabled { get; set; }

    /// <summary>Text the client can show its users, e.g. "Back at 18:00".</summary>
    public string? Message { get; set; }
}