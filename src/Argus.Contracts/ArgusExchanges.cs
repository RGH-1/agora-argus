namespace Argus.Contracts;

/// <summary>Exchange names. Constants, not options: every client must use the same ones.</summary>
public static class ArgusExchanges
{
    /// <summary>Client → Argus (topic).</summary>
    public const string Ingest = "argus.ingest";

    /// <summary>Argus → client (topic, routing keys start with the ClientId).</summary>
    public const string Outbound = "argus.outbound";

    /// <summary>Dead-letter exchange (direct).</summary>
    public const string DeadLetter = "argus.dlx";
}