using Argus.Web.Data.Entities;

namespace Argus.Web.Services;

public interface IClientAppService
{
    Task<IReadOnlyList<ClientApp>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ClientApp?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<ClientApp> CreateAsync(string clientId, string name, CancellationToken cancellationToken = default);

    /// <summary>Saves the name, suspended flag and health thresholds. False when the client doesn't exist; throws ValidationException on invalid values.</summary>
    Task<bool> UpdateAsync(string clientId, ClientAppSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Deletes the client app and all its data (charts, data points, incidents, errors). False when it doesn't exist.</summary>
    Task<bool> DeleteAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Switches maintenance mode and sends MaintenanceModeChanged to the client. False when the client doesn't exist.</summary>
    Task<bool> SetMaintenanceAsync(string clientId, bool enabled, string? message, CancellationToken cancellationToken = default);
}