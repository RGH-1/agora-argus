using Argus.Contracts;

namespace Argus.Web.Services;

public interface IHealthService
{
    /// <summary>Stores one message from argus.health (heartbeat, start, stop or error).</summary>
    Task ProcessAsync(ArgusMessage message, CancellationToken cancellationToken = default);
}