using Argus.Contracts.Outbound;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Argus.Contracts.Tests;

[Trait("Category", "Integration")]
public class ArgusConnectionTests
{
    [Fact]
    public async Task Connects_and_declares_the_topology()
    {
        const string clientId = "connection-test";
        var argus = await TestBroker.StartArgusConnectionAsync(clientId);
        try
        {
            var connection = await argus.GetAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(connection.IsOpen);
            Assert.Same(connection, argus.Current);

            await using var channel = await connection.CreateChannelAsync();
            await channel.QueueDeclarePassiveAsync(ArgusQueues.Inbox(clientId)); // throws if it wasn't declared
            await channel.QueueDeleteAsync(ArgusQueues.Inbox(clientId));
            await channel.QueueDeleteAsync(ArgusQueues.ClientDead(clientId));
        }
        finally
        {
            await argus.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Unreachable_broker_never_throws_and_publishing_fails_fast()
    {
        var nothingListening = TestBroker.Options;
        nothingListening.Port = 1;

        var argus = await TestBroker.StartArgusConnectionAsync("offline-test", nothingListening); // returns at once
        await Task.Delay(500);

        Assert.Null(argus.Current);
        Assert.False(argus.GetAsync(CancellationToken.None).IsCompleted); // still retrying in the background

        // The app's request is never held up waiting for the broker: the publish throws straight away.
        await using var publisher = new ArgusPublisher(argus, NullLogger<ArgusPublisher>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(ArgusExchanges.Outbound, "offline-test.health.status", new HealthStatusChanged()));

        await argus.StopAsync(CancellationToken.None); // stopping while retrying is clean
    }
}