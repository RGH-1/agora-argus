using Argus.Web.Data.Entities;

namespace Argus.Web.Repositories;

/// <summary>Reads for the dashboard (§4.3): a client's charts and the data points they are built from.</summary>
public interface IMetricRepository
{
    /// <summary>The client's charts in the order they were created.</summary>
    Task<IReadOnlyList<MetricDefinition>> GetDefinitionsAsync(int clientAppId, CancellationToken cancellationToken = default);

    /// <summary>The client's data points with this name from fromUtc (included) to toUtc (excluded).</summary>
    Task<IReadOnlyList<DataPoint>> GetPointsAsync(int clientAppId, string name, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}