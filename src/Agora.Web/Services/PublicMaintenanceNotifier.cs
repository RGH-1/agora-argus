using Agora.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Agora.Web.Services;

/// <summary>Best-effort browser notification. Persistence is always completed before this is called.</summary>
public sealed class PublicMaintenanceNotifier(IHubContext<MaintenanceStatusHub> hub, ILogger<PublicMaintenanceNotifier> logger) : IPublicMaintenanceNotifier
{
    public async Task StatusChangedAsync(MaintenanceStatus status)
    {
        try { await hub.Clients.All.SendAsync("MaintenanceStatusChanged", status); }
        catch (Exception exception) { logger.LogWarning(exception, "Maintenance status was persisted but could not be broadcast"); }
    }
}
