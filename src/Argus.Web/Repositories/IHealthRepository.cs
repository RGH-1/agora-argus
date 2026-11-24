using Argus.Web.Data.Entities;

namespace Argus.Web.Repositories;

/// <summary>Writes for incoming health messages. Nothing is stored until SaveAsync, which saves it all in one transaction.</summary>
public interface IHealthRepository
{
    Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    void MarkProcessed(Guid messageId);
    void AddDataPoints(IEnumerable<DataPoint> dataPoints);
    void AddError(ErrorEvent errorEvent);
    Task SaveAsync(CancellationToken cancellationToken = default);
}