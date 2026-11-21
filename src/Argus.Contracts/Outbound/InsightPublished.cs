namespace Argus.Contracts.Outbound;

/// <summary>Argus → client, routing key {clientId}.insights.published. An AI suggestion; advisory only.</summary>
public class InsightPublished : ArgusMessage
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public InsightSeverity Severity { get; set; }
}