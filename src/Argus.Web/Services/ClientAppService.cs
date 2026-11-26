using Argus.Contracts;
using Argus.Contracts.Outbound;
using Argus.Web.Data.Entities;
using Argus.Web.Repositories;

namespace Argus.Web.Services;

public class ClientAppService(IClientAppRepository clientAppRepository, IClientNotifier notifier) : IClientAppService
{
    public Task<IReadOnlyList<ClientApp>> GetAllAsync(CancellationToken cancellationToken = default) =>
        clientAppRepository.GetAllAsync(cancellationToken);

    public Task<ClientApp?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default) =>
        clientAppRepository.GetByClientIdAsync(clientId, cancellationToken);

    public async Task<ClientApp> CreateAsync(string clientId, string name, CancellationToken cancellationToken = default)
    {
        clientId = clientId?.Trim() ?? string.Empty;
        if (!ArgusClientId.IsValid(clientId))
            throw new ArgumentException("ClientId must be 1-50 lowercase letters, digits or '-'.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (await clientAppRepository.ExistsAsync(clientId, cancellationToken))
            throw new InvalidOperationException($"A client app with ClientId '{clientId}' already exists.");

        var clientApp = new ClientApp { ClientId = clientId, Name = name.Trim() };
        await clientAppRepository.AddAsync(clientApp, cancellationToken);
        return clientApp;
    }

    public async Task<bool> SetMaintenanceAsync(string clientId, bool enabled, string? message, CancellationToken cancellationToken = default)
    {
        if (message?.Length > 200) throw new ArgumentException("The maintenance message can be at most 200 characters.", nameof(message));

        var client = await clientAppRepository.GetByClientIdAsync(clientId, cancellationToken);
        if (client is null) return false;

        client.InMaintenance = enabled;
        client.MaintenanceMessage = enabled && !string.IsNullOrWhiteSpace(message) ? message.Trim() : null;
        await clientAppRepository.SaveAsync(cancellationToken);

        // Saved first, so Argus and the client never disagree about a switch that didn't happen.
        await notifier.SendAsync(client.ClientId,
            new MaintenanceModeChanged { Enabled = enabled, Message = client.MaintenanceMessage }, cancellationToken);
        return true;
    }
}