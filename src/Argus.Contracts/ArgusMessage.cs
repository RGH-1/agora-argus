namespace Argus.Contracts;

/// <summary>
/// Base of every message that travels between Argus and its clients.
/// </summary>
public abstract class ArgusMessage
{
    /// <summary>Unique per message; consumers use it to skip duplicates.</summary>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>The client the message is from (client → Argus) or for (Argus → client), e.g. "agora".</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>When it happened at the sender, in UTC.</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}