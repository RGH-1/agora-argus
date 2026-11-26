using Argus.Contracts;
using Argus.Web.Services;

namespace Argus.Web.Tests;

/// <summary>Records what Argus would send to clients, so tests don't need RabbitMQ.</summary>
public class FakeClientNotifier : IClientNotifier
{
    public List<(string ClientId, ArgusMessage Message)> Sent { get; } = [];

    public Task SendAsync(string clientId, ArgusMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add((clientId, message));
        return Task.CompletedTask;
    }
}