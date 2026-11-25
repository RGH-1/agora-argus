using Argus.Web.Data.Entities;

namespace Argus.Web.Repositories;

public interface IIncidentRepository
{
    /// <summary>The client's incident that is still Open or Acknowledged, if any. There is at most one.</summary>
    Task<Incident?> GetOpenAsync(int clientAppId, CancellationToken cancellationToken = default);

    /// <summary>Newest first, with their client. All clients when clientId is null.</summary>
    Task<IReadOnlyList<Incident>> GetAllAsync(string? clientId, CancellationToken cancellationToken = default);

    Task<Incident?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    void Add(Incident incident);
    Task SaveAsync(CancellationToken cancellationToken = default);
}