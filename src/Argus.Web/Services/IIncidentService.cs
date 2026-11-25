using Argus.Web.Data.Entities;

namespace Argus.Web.Services;

/// <summary>What operators do with incidents. Argus opens and closes them itself in the HealthService.</summary>
public interface IIncidentService
{
    Task<IReadOnlyList<Incident>> GetAllAsync(string? clientId, CancellationToken cancellationToken = default);
    Task<Incident?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Incident?> GetOpenAsync(int clientAppId, CancellationToken cancellationToken = default);

    // Each returns false when the incident doesn't exist.
    Task<bool> AcknowledgeAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ResolveAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> SaveNotesAsync(int id, string? notes, CancellationToken cancellationToken = default);
}