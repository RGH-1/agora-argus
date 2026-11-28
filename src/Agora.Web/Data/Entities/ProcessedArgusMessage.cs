namespace Agora.Web.Data.Entities;

/// <summary>Durable inbox de-duplication record for messages whose state is not itself an event history.</summary>
public sealed class ProcessedArgusMessage
{
    public Guid MessageId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; set; }
}
