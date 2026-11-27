using Argus.Web.Data.Entities;

namespace Argus.Web.Repositories;

/// <summary>Writes for incoming health messages. Nothing is stored until SaveAsync, which saves it all in one transaction.</summary>
public interface IHealthRepository
{
    Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    void MarkProcessed(Guid messageId);
    void AddDataPoints(IEnumerable<DataPoint> dataPoints);
    void AddError(ErrorEvent errorEvent);

    /// <summary>The client's charts, to see which data names already have one.</summary>
    Task<IReadOnlyList<MetricDefinition>> GetMetricDefinitionsAsync(int clientAppId, CancellationToken cancellationToken = default);
    void AddMetricDefinitions(IEnumerable<MetricDefinition> metricDefinitions);
    /// <summary>Clients the watchdog must check: not suspended and last seen Healthy or Degraded.</summary>
    Task<IReadOnlyList<ClientApp>> GetLiveClientsAsync(CancellationToken cancellationToken = default);

    /// <summary>The sys.* numbers of the client's latest heartbeat, e.g. "sys.memory_mb" → 182. Empty before the first one.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetLatestHeartbeatAsync(int clientAppId, CancellationToken cancellationToken = default);


    /// <summary>The client's newest errors first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<ErrorEvent>> GetLatestErrorsAsync(int clientAppId, int limit, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}