using Argus.Contracts;
using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Repositories;

public class HealthRepository(ArgusDbContext dbContext) : IHealthRepository
{
    public Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        dbContext.ProcessedMessages.AnyAsync(processed => processed.MessageId == messageId, cancellationToken);

    public void MarkProcessed(Guid messageId) =>
        dbContext.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId, ProcessedAtUtc = DateTime.UtcNow });

    public void AddDataPoints(IEnumerable<DataPoint> dataPoints) => dbContext.DataPoints.AddRange(dataPoints);

    public void AddError(ErrorEvent errorEvent) => dbContext.ErrorEvents.Add(errorEvent);

    public async Task<IReadOnlyList<ClientApp>> GetLiveClientsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ClientApps
            .Where(app => !app.IsSuspended && (app.Health == HealthStatus.Healthy || app.Health == HealthStatus.Degraded))
            .ToListAsync(cancellationToken);


    public async Task<IReadOnlyList<MetricDefinition>> GetMetricDefinitionsAsync(int clientAppId, CancellationToken cancellationToken = default) =>
        await dbContext.MetricDefinitions.AsNoTracking().Where(metric => metric.ClientAppId == clientAppId).ToListAsync(cancellationToken);

    public void AddMetricDefinitions(IEnumerable<MetricDefinition> metricDefinitions) => dbContext.MetricDefinitions.AddRange(metricDefinitions);

    public async Task<IReadOnlyDictionary<string, decimal>> GetLatestHeartbeatAsync(int clientAppId, CancellationToken cancellationToken = default)
    {
        // All numbers of one heartbeat share its timestamp, and sys.uptime_sec is in every heartbeat.
        var latest = await dbContext.DataPoints
            .Where(point => point.ClientAppId == clientAppId && point.Name == "sys.uptime_sec")
            .MaxAsync(point => (DateTime?)point.TimestampUtc, cancellationToken);
        if (latest is null) return new Dictionary<string, decimal>();

        return await dbContext.DataPoints
            .Where(point => point.ClientAppId == clientAppId && point.TimestampUtc == latest && point.Name.StartsWith("sys."))
            .ToDictionaryAsync(point => point.Name, point => point.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorEvent>> GetLatestErrorsAsync(int clientAppId, int limit, CancellationToken cancellationToken = default) =>
        await dbContext.ErrorEvents.AsNoTracking()
            .Where(error => error.ClientAppId == clientAppId) // the (ClientAppId, OccurredAtUtc) index serves this and the sort
            .OrderByDescending(error => error.OccurredAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);


    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}