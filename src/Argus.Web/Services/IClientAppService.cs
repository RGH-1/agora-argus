using Argus.Web.Data.Entities;

namespace Argus.Web.Services;

public interface IClientAppService
{
    Task<IReadOnlyList<ClientApp>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ClientApp?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<ClientApp> CreateAsync(string clientId, string name, CancellationToken cancellationToken = default);
}