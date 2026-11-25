using Argus.Web.Data.Entities;

namespace Argus.Web.Models.Status;

/// <summary>One card on the status board: the client and the numbers from its latest heartbeat.</summary>
public class ClientStatusViewModel
{
    public required ClientApp Client { get; init; }

    /// <summary>e.g. "sys.memory_mb" → 182. Empty until the client's first heartbeat.</summary>
    public required IReadOnlyDictionary<string, decimal> Latest { get; init; }
}