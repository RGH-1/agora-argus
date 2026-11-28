using System.ComponentModel.DataAnnotations;

namespace Argus.Web.Services;

/// <summary>
/// What an Admin can change on a client app (§4.1). The same rules check the form (MVC) and the service,
/// so a value can never get past one of them and not the other.
/// </summary>
public class ClientAppSettings
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>A suspended client's messages are logged and ignored.</summary>
    [Display(Name = "Suspended")]
    public bool IsSuspended { get; set; }

    // The SDK sends a heartbeat every 10 s, so a timeout under 15 s would call a healthy client Down.
    [Range(15, 3600), Display(Name = "Heartbeat timeout (s)")]
    public int HeartbeatTimeoutSec { get; set; } = 30;

    [Range(1, 60000), Display(Name = "Slow database (ms)")]
    public int SlowDbMs { get; set; } = 500;

    [Range(0, 100), Display(Name = "Max error rate (%)")]
    public int MaxErrorRatePercent { get; set; } = 5;
}