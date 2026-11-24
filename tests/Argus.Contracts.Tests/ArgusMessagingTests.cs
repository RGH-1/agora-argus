using System.Text;
using Argus.Contracts.Health;
using Argus.Contracts.Outbound;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Argus.Contracts.Tests;

/// <summary>
/// Integration tests for ArgusPublisher and ArgusConsumer: they need RabbitMQ running on localhost:5672.
/// They only touch the queues of a throwaway client, "messaging-test", and delete them at the end.
/// </summary>
[Trait("Category", "Integration")]
public class ArgusMessagingTests : IAsyncLifetime
{
    private const string TestClient = "messaging-test";
    private static readonly string Inbox = ArgusQueues.Inbox(TestClient);
    private static readonly string Dead = ArgusQueues.ClientDead(TestClient);
    private static readonly string RoutingKey = ArgusRoutingKeys.HealthStatusChanged(TestClient);

    private ArgusConnection _argus = null!;
    private IChannel _channel = null!;
    private ArgusPublisher _publisher = null!;

    public async Task InitializeAsync()
    {
        _argus = await TestBroker.StartArgusConnectionAsync(TestClient);
        var connection = await _argus.GetAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        _channel = await connection.CreateChannelAsync();
        _publisher = new ArgusPublisher(_argus, NullLogger<ArgusPublisher>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _publisher.DisposeAsync();
        await _channel.QueueDeleteAsync(Inbox);
        await _channel.QueueDeleteAsync(Dead);
        await _channel.CloseAsync();
        await _argus.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Publisher_sets_id_type_persistence_and_expiry()
    {
        var message = new HealthStatusChanged { ClientId = TestClient, NewStatus = HealthStatus.Down };

        await _publisher.PublishAsync(ArgusExchanges.Outbound, RoutingKey, message, TimeSpan.FromSeconds(30));

        var result = await TestBroker.WaitForMessageAsync(_channel, Inbox);
        Assert.Equal(message.MessageId.ToString(), result.BasicProperties.MessageId);
        Assert.Equal(nameof(HealthStatusChanged), result.BasicProperties.Type);
        Assert.True(result.BasicProperties.Persistent);
        Assert.Equal("30000", result.BasicProperties.Expiration);
    }

    [Fact]
    public async Task Published_message_is_consumed_and_acked()
    {
        // RunContinuationsAsynchronously: the test must not continue on the consumer's own thread
        var received = new TaskCompletionSource<ArgusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new TestConsumer(_argus, Inbox, message =>
        {
            received.TrySetResult(message);
            return Task.CompletedTask;
        });
        var sent = new HealthStatusChanged { ClientId = TestClient, NewStatus = HealthStatus.Down };

        await consumer.StartAsync(CancellationToken.None);
        await _publisher.PublishAsync(ArgusExchanges.Outbound, RoutingKey, sent);
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await consumer.StopAsync(CancellationToken.None);

        // It came back as the right class, and after the consumer stopped nothing went back to the queue (it was acked).
        var changed = Assert.IsType<HealthStatusChanged>(message);
        Assert.Equal(sent.MessageId, changed.MessageId);
        Assert.Equal(HealthStatus.Down, changed.NewStatus);
        Assert.Equal(0u, await _channel.MessageCountAsync(Inbox));
    }

    [Fact]
    public async Task Handler_that_always_throws_is_dead_lettered_after_3_attempts()
    {
        var attempts = 0;
        var consumer = new TestConsumer(_argus, Inbox, _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("Simulated handler failure");
        });

        await consumer.StartAsync(CancellationToken.None);
        await _publisher.PublishAsync(ArgusExchanges.Outbound, RoutingKey, new HealthStatusChanged { ClientId = TestClient });
        var dead = await TestBroker.WaitForMessageAsync(_channel, Dead);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(ArgusConsumer.MaxAttempts, attempts);
        Assert.Equal(ArgusConsumer.MaxAttempts - 1, Convert.ToInt32(dead.BasicProperties.Headers![ArgusConsumer.RetryCountHeader]));
        Assert.Equal(0u, await _channel.MessageCountAsync(Inbox));
    }

    [Fact]
    public async Task Unreadable_message_goes_straight_to_the_dead_queue()
    {
        var handled = false;
        var consumer = new TestConsumer(_argus, Inbox, _ =>
        {
            handled = true;
            return Task.CompletedTask;
        });

        await consumer.StartAsync(CancellationToken.None);
        await _channel.BasicPublishAsync(ArgusExchanges.Outbound, RoutingKey, mandatory: false,
            new BasicProperties { Type = "NoSuchMessage" }, Encoding.UTF8.GetBytes("{}"));
        await TestBroker.WaitForMessageAsync(_channel, Dead);
        await consumer.StopAsync(CancellationToken.None);

        Assert.False(handled);
    }

    private sealed class TestConsumer(ArgusConnection connection, string queue, Func<ArgusMessage, Task> handler)
        : ArgusConsumer(connection, NullLogger.Instance)
    {
        protected override string QueueName => queue;

        protected override Task HandleAsync(ArgusMessage message, CancellationToken ct) => handler(message);
    }
}