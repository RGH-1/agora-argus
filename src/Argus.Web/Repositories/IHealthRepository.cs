using Argus.Web.Data.Entities;

namespace Argus.Web.Repositories;

/// <summary>Writes for incoming health messages. Nothing is stored until SaveAsync, which saves it all in one transaction.</summary>
public interface IHealthRepository
{
    Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    void MarkProcessed(Guid messageId);
    void AddDataPoints(IEnumerable<DataPoint> dataPoints);
    void AddError(ErrorEvent errorEvent);
    /// <summary>Clients the watchdog must check: not suspended and last seen Healthy or Degraded.</summary>
    Task<IReadOnlyList<ClientApp>> GetLiveClientsAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}