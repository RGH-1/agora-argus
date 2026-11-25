using Argus.Contracts;

namespace Argus.Web.Services;

public interface IHealthService
{
    /// <summary>Stores one message from argus.health (heartbeat, start, stop or error).</summary>
    Task ProcessAsync(ArgusMessage message, CancellationToken cancellationToken = default);

    /// <summary>The sys.* numbers of the client's latest heartbeat, for the status board.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetLatestHeartbeatAsync(int clientAppId, CancellationToken cancellationToken = default);


    /// <summary>Marks every live client that has been silent longer than its heartbeat timeout as Down.</summary>
    Task MarkSilentClientsDownAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}