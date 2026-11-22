using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Argus.Contracts;

/// <summary>
/// Base class for every consumer in both systems (design draft §7.2).
/// A subclass names its queue and implements HandleAsync; this class does the RabbitMQ part:
/// prefetch, turning the JSON back into the right message class, ack, retries and dead-lettering.
/// </summary>
public abstract class ArgusConsumer(IConnection connection, ILogger logger) : BackgroundService
{
    /// <summary>Attempts per message before it goes to the dead queue.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Header that counts how many times the message already failed.</summary>
    public const string RetryCountHeader = "x-retry-count";

    private const ushort PrefetchCount = 10;

    // "Heartbeat" -> typeof(Heartbeat), for every message class in Argus.Contracts
    private static readonly Dictionary<string, Type> MessageTypes = typeof(ArgusMessage).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ArgusMessage)) && !t.IsAbstract)
        .ToDictionary(t => t.Name);

    // Held while a message is processed, so stopping can wait for it to finish.
    private readonly SemaphoreSlim _busy = new(1, 1);
    private IChannel? _channel;

    protected abstract string QueueName { get; }

    /// <summary>Process one message. Throwing means "failed": it is retried, then dead-lettered.</summary>
    protected abstract Task HandleAsync(ArgusMessage message, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Confirms, so a retry copy is safely stored before the original is acked.
        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        // At most 10 unacknowledged messages are sent to this consumer at a time.
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(_channel, delivery, stoppingToken);

        // autoAck: false = manual ack. RabbitMQ keeps the message until we say we're done with it.
        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consuming {Queue}", QueueName);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. Cancels stoppingToken: messages that arrive from now on are skipped.
        await base.StopAsync(cancellationToken);

        // 2. Waits for the message in progress to finish and be acked.
        await _busy.WaitAsync(cancellationToken);
        _busy.Release();

        // 3. Closing the channel hands every unacknowledged message back to the queue.
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        await _busy.WaitAsync();
        try
        {
            // Stopping: leave it unacked, it goes back to the queue when the channel closes.
            if (!ct.IsCancellationRequested) await ProcessAsync(channel, delivery, ct);
        }
        finally
        {
            _busy.Release();
        }
    }

    private async Task ProcessAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        var typeName = delivery.BasicProperties.Type;
        var message = Deserialize(typeName, delivery.Body);
        if (message is null)
        {
            // Retrying can't fix a message we can't read, so it goes straight to the dead queue.
            logger.LogError("Unreadable message of type {Type} on {Queue}, dead-lettering it", typeName, QueueName);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            await HandleAsync(message, ct);
        }
        catch (Exception ex)
        {
            await RetryOrDeadLetterAsync(channel, delivery, message, ex);
            return;
        }

        // Only now is the message removed from the queue.
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        logger.LogDebug("Consumed {Type} {MessageId} from {Queue}", typeName, message.MessageId, QueueName);
    }

    private async Task RetryOrDeadLetterAsync(IChannel channel, BasicDeliverEventArgs delivery, ArgusMessage message, Exception ex)
    {
        var failures = GetRetryCount(delivery.BasicProperties) + 1;

        if (failures >= MaxAttempts)
        {
            logger.LogError(ex, "{Type} {MessageId} failed {Failures} times on {Queue}, dead-lettering it",
                delivery.BasicProperties.Type, message.MessageId, failures, QueueName);
            // requeue: false -> the queue sends it to argus.dlx, which routes it to the *.dead queue
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        logger.LogWarning(ex, "{Type} {MessageId} failed on {Queue} (attempt {Failures} of {Max}), retrying",
            delivery.BasicProperties.Type, message.MessageId, QueueName, failures, MaxAttempts);

        // RabbitMQ 4 doesn't count "nack + requeue" as a failed delivery, so we count ourselves:
        // put a copy at the back of the same queue with the counter + 1, then ack the original.
        // The "" exchange is RabbitMQ's default exchange: it delivers straight to the queue named by the routing key.
        var copy = new BasicProperties(delivery.BasicProperties)
        {
            Headers = new Dictionary<string, object?>(delivery.BasicProperties.Headers ?? new Dictionary<string, object?>())
            {
                [RetryCountHeader] = failures
            }
        };
        await channel.BasicPublishAsync(exchange: "", routingKey: QueueName, mandatory: false, copy, delivery.Body);
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties) =>
        properties.Headers?.TryGetValue(RetryCountHeader, out var value) == true ? Convert.ToInt32(value) : 0;

    private static ArgusMessage? Deserialize(string? typeName, ReadOnlyMemory<byte> body)
    {
        if (typeName is null || !MessageTypes.TryGetValue(typeName, out var type)) return null;
        try
        {
            return (ArgusMessage?)JsonSerializer.Deserialize(body.Span, type, ArgusJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}