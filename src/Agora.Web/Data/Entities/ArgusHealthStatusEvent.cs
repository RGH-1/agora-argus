using Argus.Contracts;

namespace Agora.Web.Data.Entities;

public sealed class ArgusHealthStatusEvent
{
    public int Id { get; set; }
    public Guid MessageId { get; set; }
    public HealthStatus OldStatus { get; set; }
    public HealthStatus NewStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? IncidentId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
