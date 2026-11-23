using System.ComponentModel.DataAnnotations;

namespace Argus.Web.Data.Entities;

/// <summary>One value a client sent: a heartbeat number, a gauge or an activity event (§4.3).</summary>
public class DataPoint
{
    public long Id { get; set; }
    public int ClientAppId { get; set; }
    public ClientApp ClientApp { get; set; } = null!;
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    [StringLength(200)] public string? Dimension { get; set; }
    [StringLength(100)] public string? TargetId { get; set; }
    [StringLength(200)] public string? TargetLabel { get; set; }
    [StringLength(100)] public string? ActorId { get; set; }
    public DateTime TimestampUtc { get; set; }
}