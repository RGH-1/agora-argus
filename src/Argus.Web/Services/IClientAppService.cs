using Argus.Web.Data.Entities;

namespace Argus.Web.Services;

public interface IClientAppService
{
    Task<IReadOnlyList<ClientApp>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ClientApp?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<ClientApp> CreateAsync(string clientId, string name, CancellationToken cancellationToken = default);

    /// <summary>Switches maintenance mode and sends MaintenanceModeChanged to the client. False when the client doesn't exist.</summary>
    Task<bool> SetMaintenanceAsync(string clientId, bool enabled, string? message, CancellationToken cancellationToken = default);
}