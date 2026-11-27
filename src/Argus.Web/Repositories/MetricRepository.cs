using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Repositories;

public class MetricRepository(ArgusDbContext dbContext) : IMetricRepository
{
    public async Task<IReadOnlyList<MetricDefinition>> GetDefinitionsAsync(int clientAppId, CancellationToken cancellationToken = default) =>
        await dbContext.MetricDefinitions.AsNoTracking()
            .Where(metric => metric.ClientAppId == clientAppId)
            .OrderBy(metric => metric.Id)
            .ToListAsync(cancellationToken);

    // Client, name and time: exactly the DataPoints index, so SQL Server reads only the rows the chart needs.
    public async Task<IReadOnlyList<DataPoint>> GetPointsAsync(int clientAppId, string name, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        await dbContext.DataPoints.AsNoTracking()
            .Where(point => point.ClientAppId == clientAppId && point.Name == name
                && point.TimestampUtc >= fromUtc && point.TimestampUtc < toUtc)
            .ToListAsync(cancellationToken);
}