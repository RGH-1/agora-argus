using Agora.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace Agora.Web.Hubs;

/// <summary>Public, read-only channel for the non-sensitive maintenance banner.</summary>
public sealed class MaintenanceStatusHub(IMaintenanceStateService maintenance) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("MaintenanceStatusChanged", await maintenance.GetAsync(Context.ConnectionAborted), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
