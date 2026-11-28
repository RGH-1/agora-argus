using Argus.Contracts;
using Argus.Contracts.Outbound;
using Argus.Web.Data.Entities;
using Argus.Web.Services;

namespace Argus.Web.Tests;

/// <summary>Records what Argus would push to the open pages, so tests don't need SignalR.</summary>
public class FakeStatusBoardNotifier : IStatusBoardNotifier
{
    public int BoardChanges { get; private set; }
    public List<(string ClientId, HealthStatus Status, int? IncidentId, bool IncidentResolved)> StatusChanges { get; } = [];

    public Task BoardChangedAsync(CancellationToken cancellationToken = default)
    {
        BoardChanges++;
        return Task.CompletedTask;
    }

    public Task StatusChangedAsync(ClientApp client, HealthStatusChanged change, Incident? incident, CancellationToken cancellationToken = default)
    {
        StatusChanges.Add((client.ClientId, change.NewStatus, incident?.Id, incident?.Status == IncidentStatus.Resolved));
        return Task.CompletedTask;
    }
}