using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Argus.Contracts;

/// <summary>
/// Sends Argus messages as JSON (design draft §7.3). One instance per app (singleton).
/// Each publish waits for the broker's confirmation, so when it returns, RabbitMQ has the message.
/// While RabbitMQ is unreachable it throws straight away instead of waiting: callers log it and move on.
/// </summary>
public sealed class ArgusPublisher(ArgusConnection connection, ILogger<ArgusPublisher> logger) : IAsyncDisposable
{
    // A channel must not be used by two threads at once, so publishes take turns.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    /// <param name="expiration">Drop the message if nobody consumed it in time (used for heartbeats).</param>
    public async Task PublishAsync(string exchange, string routingKey, ArgusMessage message,
        TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), ArgusJson.Options);
        var properties = new BasicProperties
        {
            Persistent = true,                          // written to disk, survives a broker restart
            ContentType = "application/json",
            Type = message.GetType().Name,              // the consumer uses it to pick the class
            MessageId = message.MessageId.ToString(),
            Expiration = expiration?.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture),
        };

        await _lock.WaitAsync(ct);
        try
        {
            // (Re)open the channel if needed: a broker error closes it, e.g. publishing to a missing exchange.
            // Publisher confirms: BasicPublishAsync only returns once RabbitMQ has stored the message.
            if (_channel is not { IsOpen: true })
            {
                var open = connection.Current ?? throw new InvalidOperationException("Not connected to RabbitMQ yet; the message was not sent.");
                _channel = await open.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
            }

            await _channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, body, ct);
        }
        finally
        {
            _lock.Release();
        }

        logger.LogDebug("Published {Type} {MessageId} to {Exchange} with key {RoutingKey}",
            properties.Type, properties.MessageId, exchange, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        _lock.Dispose();
    }
}