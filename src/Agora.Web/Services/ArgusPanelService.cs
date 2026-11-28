using Agora.Web.Data;
using Agora.Web.Models.Moderation;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Services;

public sealed class ArgusPanelService(AgoraDbContext db, IMaintenanceStateService maintenance) : IArgusPanelService
{
    public async Task<ArgusPanelViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        // SQLite (used by the focused tests) cannot translate DateTimeOffset ordering. First bound by
        // receive order in SQL, then apply the user-facing event-time ordering consistently in memory.
        var health = (await db.ArgusHealthStatusEvents.AsNoTracking().OrderByDescending(item => item.Id).Take(200).ToListAsync(cancellationToken))
            .OrderByDescending(item => item.OccurredAtUtc).Take(50).ToList();
        var insights = (await db.ArgusInsights.AsNoTracking().OrderByDescending(item => item.Id).Take(100).ToListAsync(cancellationToken))
            .OrderByDescending(item => item.OccurredAtUtc).Take(25).ToList();
        return new ArgusPanelViewModel(await maintenance.GetAsync(cancellationToken), health, insights);
    }
}
