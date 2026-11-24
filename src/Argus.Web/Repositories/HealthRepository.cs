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

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}