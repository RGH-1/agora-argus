using System.Text;
using RabbitMQ.Client;

namespace Argus.Contracts.Tests;

/// <summary>
/// Integration tests: they need RabbitMQ running on localhost:5672.
/// They only touch the queues of a throwaway client, "topology-test", and delete them at the end.
/// </summary>
[Trait("Category", "Integration")]
public class ArgusTopologyTests : IAsyncLifetime
{
    private const string TestClient = "topology-test";

    private IConnection _connection = null!;
    private IChannel _channel = null!;

    public async Task InitializeAsync()
    {
        _connection = await TestBroker.ConnectAsync();
        _channel = await _connection.CreateChannelAsync();
    }

    public async Task DisposeAsync()
    {
        await _channel.QueueDeleteAsync(ArgusQueues.Inbox(TestClient));
        await _channel.QueueDeleteAsync(ArgusQueues.ClientDead(TestClient));
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }

    [Fact]
    public async Task Declaring_twice_causes_no_error()
    {
        await ArgusTopology.DeclareIngestAsync(_channel);
        await ArgusTopology.DeclareIngestAsync(_channel);
        await ArgusTopology.DeclareClientInboxAsync(_channel, TestClient);
        await ArgusTopology.DeclareClientInboxAsync(_channel, TestClient);

        // Passive declare throws if the queue doesn't exist
        foreach (var queue in new[] { ArgusQueues.Health, ArgusQueues.Analytics, ArgusQueues.Dead,
                                      ArgusQueues.Inbox(TestClient), ArgusQueues.ClientDead(TestClient) })
        {
            await _channel.QueueDeclarePassiveAsync(queue);
        }
    }

    [Fact]
    public async Task Outbound_message_for_a_client_lands_in_its_inbox()
    {
        await ArgusTopology.DeclareClientInboxAsync(_channel, TestClient);

        await PublishAsync(ArgusExchanges.Outbound, ArgusRoutingKeys.HealthStatusChanged(TestClient));

        await TestBroker.WaitForMessageAsync(_channel, ArgusQueues.Inbox(TestClient));
    }

    [Fact]
    public async Task Rejected_message_goes_to_the_dead_queue()
    {
        await ArgusTopology.DeclareClientInboxAsync(_channel, TestClient);
        await PublishAsync(ArgusExchanges.Outbound, ArgusRoutingKeys.HealthStatusChanged(TestClient));

        var delivery = await TestBroker.WaitForMessageAsync(_channel, ArgusQueues.Inbox(TestClient), autoAck: false);
        await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);

        await TestBroker.WaitForMessageAsync(_channel, ArgusQueues.ClientDead(TestClient));
    }

    [Fact]
    public async Task Message_that_keeps_crashing_its_consumer_is_dead_lettered()
    {
        await ArgusTopology.DeclareClientInboxAsync(_channel, TestClient);
        await PublishAsync(ArgusExchanges.Outbound, ArgusRoutingKeys.HealthStatusChanged(TestClient));

        // A consumer that dies mid-message (channel closed, no ack) is what x-delivery-limit counts.
        // RabbitMQ 4 does NOT count a nack with requeue: true, so that alone would loop forever.
        var attempts = 0;
        for (var i = 0; i < 100; i++)
        {
            var consumerChannel = await _connection.CreateChannelAsync();
            var delivery = await consumerChannel.BasicGetAsync(ArgusQueues.Inbox(TestClient), autoAck: false);
            await consumerChannel.CloseAsync(); // "crash": the message goes back unacknowledged

            if (delivery is not null)
            {
                attempts++;
                continue;
            }

            // Not in the inbox: either still on its way back, or dead-lettered
            if (await _channel.BasicGetAsync(ArgusQueues.ClientDead(TestClient), autoAck: true) is not null) break;
            await Task.Delay(100);
        }

        Assert.Equal(ArgusTopology.DeliveryLimit + 1, attempts); // the first delivery + 3 redeliveries
    }

    private async Task PublishAsync(string exchange, string routingKey)
    {
        await _channel.BasicPublishAsync(exchange, routingKey, mandatory: true,
            basicProperties: new BasicProperties(), body: Encoding.UTF8.GetBytes("{}"));
    }
}