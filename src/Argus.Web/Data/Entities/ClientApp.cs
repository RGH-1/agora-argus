using System.ComponentModel.DataAnnotations;
using Argus.Contracts;

namespace Argus.Web.Data.Entities;

/// <summary>A registered client application (tenant), e.g. "agora" or "demo".</summary>
public class ClientApp
{
    public int Id { get; set; }

    /// <summary>The id the client sends in every message; also part of its queue names and routing keys.</summary>
    [Required, StringLength(50)] public string ClientId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>Messages from a suspended client are logged and ignored.</summary>
    public bool IsSuspended { get; set; }

    // Health thresholds (§4.1)
    public int HeartbeatTimeoutSec { get; set; } = 30;
    public int SlowDbMs { get; set; } = 500;
    public int MaxErrorRatePercent { get; set; } = 5;

    // Current health, kept up to date by the health consumer and the watchdog
    public HealthStatus Health { get; set; } = HealthStatus.Unknown;
    public DateTime? LastHeartbeatAtUtc { get; set; }
    [StringLength(50)] public string? Version { get; set; }

    // Maintenance mode (§4.2): switched by an operator, sent to the client as MaintenanceModeChanged
    public bool InMaintenance { get; set; }
    [StringLength(200)] public string? MaintenanceMessage { get; set; }
}