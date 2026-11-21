namespace Argus.Contracts.Outbound;

/// <summary>Argus → client, routing key {clientId}.health.status. The client's status changed in Argus.</summary>
public class HealthStatusChanged : ArgusMessage
{
    public HealthStatus OldStatus { get; set; }
    public HealthStatus NewStatus { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>The incident opened or closed by this change, if any.</summary>
    public int? IncidentId { get; set; }
}