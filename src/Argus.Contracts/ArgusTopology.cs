using RabbitMQ.Client;

namespace Argus.Contracts;

/// <summary>
/// Declares every exchange, queue and binding (design draft §7.1).
/// Argus and every client SDK run the same code at startup: declaring something that already
/// exists with identical settings does nothing, so whichever side starts first, nothing is lost.
/// </summary>
public static class ArgusTopology
{
    /// <summary>Failed deliveries a queue allows before it dead-letters the message.</summary>
    public const int DeliveryLimit = 3;

    /// <summary>The three exchanges and Argus's own queues: argus.health, argus.analytics, argus.dead.</summary>
    public static async Task DeclareIngestAsync(IChannel channel, CancellationToken ct = default)
    {
        await DeclareExchangesAsync(channel, ct);

        await DeclareQueueAsync(channel, ArgusQueues.Dead, deadLetterRoutingKey: null, ct);
        await channel.QueueBindAsync(ArgusQueues.Dead, ArgusExchanges.DeadLetter, ArgusQueues.Dead, cancellationToken: ct);

        await DeclareQueueAsync(channel, ArgusQueues.Health, ArgusQueues.Dead, ct);
        await channel.QueueBindAsync(ArgusQueues.Health, ArgusExchanges.Ingest, ArgusRoutingKeys.AllHealth, cancellationToken: ct);

        await DeclareQueueAsync(channel, ArgusQueues.Analytics, ArgusQueues.Dead, ct);
        await channel.QueueBindAsync(ArgusQueues.Analytics, ArgusExchanges.Ingest, ArgusRoutingKeys.AllAnalytics, cancellationToken: ct);
    }

    /// <summary>One client's queues: {clientId}.inbox (everything Argus sends it) and {clientId}.dead.</summary>
    public static async Task DeclareClientInboxAsync(IChannel channel, string clientId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        await DeclareExchangesAsync(channel, ct);

        var inbox = ArgusQueues.Inbox(clientId);
        var dead = ArgusQueues.ClientDead(clientId);

        await DeclareQueueAsync(channel, dead, deadLetterRoutingKey: null, ct);
        await channel.QueueBindAsync(dead, ArgusExchanges.DeadLetter, dead, cancellationToken: ct);

        await DeclareQueueAsync(channel, inbox, dead, ct);
        await channel.QueueBindAsync(inbox, ArgusExchanges.Outbound, ArgusRoutingKeys.AllForClient(clientId), cancellationToken: ct);
    }

    private static async Task DeclareExchangesAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(ArgusExchanges.Ingest, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(ArgusExchanges.Outbound, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(ArgusExchanges.DeadLetter, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
    }

    /// <summary>
    /// A durable quorum queue. With a dead-letter routing key, a message that fails more than
    /// DeliveryLimit times goes to argus.dlx with that key; without one, it's a dead queue itself.
    /// </summary>
    private static Task DeclareQueueAsync(IChannel channel, string name, string? deadLetterRoutingKey, CancellationToken ct)
    {
        var arguments = new Dictionary<string, object?> { ["x-queue-type"] = "quorum" };

        if (deadLetterRoutingKey is not null)
        {
            arguments["x-delivery-limit"] = DeliveryLimit;
            arguments["x-dead-letter-exchange"] = ArgusExchanges.DeadLetter;
            arguments["x-dead-letter-routing-key"] = deadLetterRoutingKey;
        }

        return channel.QueueDeclareAsync(name, durable: true, exclusive: false, autoDelete: false,
            arguments: arguments, cancellationToken: ct);
    }
}