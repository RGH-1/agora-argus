namespace Agora.Web.Data.Entities;

/// <summary>Singleton durable state received from Argus. It deliberately survives application restarts.</summary>
public sealed class MaintenanceState
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public bool IsEnabled { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? LastEventOccurredAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
