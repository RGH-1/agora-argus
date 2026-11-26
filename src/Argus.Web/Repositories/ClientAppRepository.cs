using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Repositories;

public class ClientAppRepository(ArgusDbContext dbContext) : IClientAppRepository
{
    public async Task<IReadOnlyList<ClientApp>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ClientApps.AsNoTracking().OrderBy(app => app.Name).ToListAsync(cancellationToken);

    public Task<ClientApp?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        dbContext.ClientApps.FirstOrDefaultAsync(app => app.ClientId == clientId, cancellationToken);

    public Task<bool> ExistsAsync(string clientId, CancellationToken cancellationToken = default) =>
        dbContext.ClientApps.AnyAsync(app => app.ClientId == clientId, cancellationToken);

    public async Task AddAsync(ClientApp clientApp, CancellationToken cancellationToken = default)
    {
        await dbContext.ClientApps.AddAsync(clientApp, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}