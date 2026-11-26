using Argus.Contracts;
using Argus.Contracts.Outbound;

namespace Argus.Web.Services;

/// <summary>Sends Argus → client messages to the client's inbox, through argus.outbound (design draft §6).</summary>
public interface IClientNotifier
{
    /// <summary>Never throws: if RabbitMQ is down the client misses this one message, which is logged.</summary>
    Task SendAsync(string clientId, ArgusMessage message, CancellationToken cancellationToken = default);
}

public class ClientNotifier(ArgusPublisher publisher, ILogger<ClientNotifier> logger) : IClientNotifier
{
    public async Task SendAsync(string clientId, ArgusMessage message, CancellationToken cancellationToken = default)
    {
        message.ClientId = clientId;
        var routingKey = message switch
        {
            HealthStatusChanged => ArgusRoutingKeys.HealthStatusChanged(clientId),
            MaintenanceModeChanged => ArgusRoutingKeys.MaintenanceModeChanged(clientId),
            InsightPublished => ArgusRoutingKeys.InsightPublished(clientId),
            _ => throw new ArgumentException($"{message.GetType().Name} is not an Argus → client message.", nameof(message)),
        };

        try
        {
            await publisher.PublishAsync(ArgusExchanges.Outbound, routingKey, message, ct: cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // shortcut: the change is already saved, so only this notification is lost; an outbox would resend it (stretch AA-67).
            logger.LogWarning("{Type} for '{ClientId}' was not sent: {Error}", message.GetType().Name, clientId, ex.Message);
        }
    }
}