using Argus.Contracts;

namespace Agora.Web.Data.Entities;

public sealed class ArgusInsight
{
    public int Id { get; set; }
    public Guid MessageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public InsightSeverity Severity { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
