using Argus.Web.Data.Entities;

namespace Argus.Web.Repositories;

public interface IClientAppRepository
{
    Task<IReadOnlyList<ClientApp>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ClientApp?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string clientId, CancellationToken cancellationToken = default);
    Task AddAsync(ClientApp clientApp, CancellationToken cancellationToken = default);
}