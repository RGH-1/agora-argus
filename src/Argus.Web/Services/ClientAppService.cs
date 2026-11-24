using Argus.Contracts;
using Argus.Web.Data.Entities;
using Argus.Web.Repositories;

namespace Argus.Web.Services;

public class ClientAppService(IClientAppRepository clientAppRepository) : IClientAppService
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
}