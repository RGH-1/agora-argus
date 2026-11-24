using Argus.Contracts;
using Microsoft.Extensions.Logging;

namespace Argus.Client;

/// <summary>
/// Sends a message from this client to Argus. It never throws: if RabbitMQ is unreachable the message
/// is logged as not sent and the app carries on (design draft §5.2: a missed heartbeat is acceptable).
/// </summary>
public sealed class ArgusSender(ArgusOptions options, ArgusPublisher publisher, ILogger<ArgusSender> logger)
{
    public async Task SendAsync(string routingKey, ArgusMessage message, TimeSpan? expiration = null)
    {
        message.ClientId = options.ClientId;
        try
        {
            await publisher.PublishAsync(ArgusExchanges.Ingest, routingKey, message, expiration);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{Type} was not sent to Argus: {Error}", message.GetType().Name, ex.Message);
        }
    }
}