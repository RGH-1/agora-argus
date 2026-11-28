using Argus.Contracts.Outbound;
using Argus.Web.Data.Entities;
using Argus.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Argus.Web.Services;

/// <summary>Tells the open Argus pages what changed, through the StatusHub (SignalR). Both methods never throw.</summary>
public interface IStatusBoardNotifier
{
    /// <summary>A heartbeat, start or stop changed the numbers on the board: open status boards reload their cards.</summary>
    Task BoardChangedAsync(CancellationToken cancellationToken = default);

    /// <summary>A client's health changed: every open page shows an alert, with the incident it opened or resolved.</summary>
    Task StatusChangedAsync(ClientApp client, HealthStatusChanged change, Incident? incident, CancellationToken cancellationToken = default);
}

public class StatusBoardNotifier(IHubContext<StatusHub> hub, ILogger<StatusBoardNotifier> logger) : IStatusBoardNotifier
{
    public Task BoardChangedAsync(CancellationToken cancellationToken = default) =>
        SendAsync("BoardChanged", [], cancellationToken);

    public Task StatusChangedAsync(ClientApp client, HealthStatusChanged change, Incident? incident, CancellationToken cancellationToken = default) =>
        SendAsync("StatusChanged",
        [
            new
            {
                client.ClientId,
                client.Name,
                Status = change.NewStatus.ToString(),
                change.Reason,
                IncidentId = incident?.Id,
                IncidentResolved = incident?.Status == IncidentStatus.Resolved,
            },
        ], cancellationToken);

    private async Task SendAsync(string method, object?[] args, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.All.SendCoreAsync(method, args, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The change is saved either way; an open page only misses this push and catches up on the next one.
            logger.LogWarning("Live update {Method} was not sent: {Error}", method, ex.Message);
        }
    }
}