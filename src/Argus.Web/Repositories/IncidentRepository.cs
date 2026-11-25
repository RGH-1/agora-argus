using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Repositories;

public class IncidentRepository(ArgusDbContext dbContext) : IIncidentRepository
{
    public Task<Incident?> GetOpenAsync(int clientAppId, CancellationToken cancellationToken = default) =>
        dbContext.Incidents.FirstOrDefaultAsync(
            incident => incident.ClientAppId == clientAppId && incident.Status != IncidentStatus.Resolved, cancellationToken);

    public async Task<IReadOnlyList<Incident>> GetAllAsync(string? clientId, CancellationToken cancellationToken = default) =>
        await dbContext.Incidents.AsNoTracking()
            .Include(incident => incident.ClientApp)
            .Where(incident => clientId == null || incident.ClientApp.ClientId == clientId)
            .OrderByDescending(incident => incident.OpenedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<Incident?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Incidents.Include(incident => incident.ClientApp)
            .FirstOrDefaultAsync(incident => incident.Id == id, cancellationToken);

    public void Add(Incident incident) => dbContext.Incidents.Add(incident);

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}