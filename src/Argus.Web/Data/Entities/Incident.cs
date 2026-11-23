using System.ComponentModel.DataAnnotations;
using Argus.Contracts;

namespace Argus.Web.Data.Entities;

/// <summary>Opened automatically when a client goes Down or Degraded, closed on recovery (§4.2).</summary>
public class Incident
{
    public int Id { get; set; }
    public int ClientAppId { get; set; }
    public ClientApp ClientApp { get; set; } = null!;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    /// <summary>The health status that opened it: Down or Degraded.</summary>
    public HealthStatus Cause { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}