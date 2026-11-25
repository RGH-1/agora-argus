using Argus.Web.Data.Entities;
using Argus.Web.Repositories;

namespace Argus.Web.Services;

public class IncidentService(IIncidentRepository incidents) : IIncidentService
{
    public Task<IReadOnlyList<Incident>> GetAllAsync(string? clientId, CancellationToken cancellationToken = default) =>
        incidents.GetAllAsync(clientId, cancellationToken);

    public Task<Incident?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        incidents.GetByIdAsync(id, cancellationToken);

    public Task<Incident?> GetOpenAsync(int clientAppId, CancellationToken cancellationToken = default) =>
        incidents.GetOpenAsync(clientAppId, cancellationToken);

    /// <summary>"Someone is on it": only an Open incident can be acknowledged.</summary>
    public Task<bool> AcknowledgeAsync(int id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, incident =>
        {
            if (incident.Status == IncidentStatus.Open) incident.Status = IncidentStatus.Acknowledged;
        }, cancellationToken);

    /// <summary>Closed by hand, e.g. a false alarm. If the client is still unhealthy, its next problem opens a new incident.</summary>
    public Task<bool> ResolveAsync(int id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, incident =>
        {
            if (incident.Status == IncidentStatus.Resolved) return;
            incident.Status = IncidentStatus.Resolved;
            incident.ResolvedAtUtc = DateTime.UtcNow;
        }, cancellationToken);

    public Task<bool> SaveNotesAsync(int id, string? notes, CancellationToken cancellationToken = default)
    {
        if (notes?.Length > 2000) throw new ArgumentException("Notes can be at most 2000 characters.", nameof(notes));
        return UpdateAsync(id, incident => incident.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), cancellationToken);
    }

    private async Task<bool> UpdateAsync(int id, Action<Incident> change, CancellationToken cancellationToken)
    {
        var incident = await incidents.GetByIdAsync(id, cancellationToken);
        if (incident is null) return false;
        change(incident);
        await incidents.SaveAsync(cancellationToken);
        return true;
    }
}