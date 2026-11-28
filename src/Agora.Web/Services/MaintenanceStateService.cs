using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Services;

public sealed class MaintenanceStateService(AgoraDbContext db, ILogger<MaintenanceStateService>? logger = null) : IMaintenanceStateService
{
    public async Task<MaintenanceStatus> GetAsync(CancellationToken cancellationToken = default)
    {
        var state = await db.MaintenanceStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == MaintenanceState.SingletonId, cancellationToken);
        if (state is not null) return new MaintenanceStatus(state.IsEnabled, state.Message, state.UpdatedAtUtc);

        // A migrated database always has this row. Fail closed if it is unexpectedly absent: returning OFF
        // would permit business writes while the actual Argus maintenance state is unknown.
        logger?.LogCritical("Maintenance-state singleton row {MaintenanceStateId} is missing", MaintenanceState.SingletonId);
        return new MaintenanceStatus(true, "Maintenance status is temporarily unavailable. Business actions are temporarily unavailable.", null);
    }
}
