using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Argus.Contracts.Tests;

/// <summary>Shared helpers for the integration tests that talk to RabbitMQ on localhost:5672.</summary>
internal static class TestBroker
{
    public static RabbitMqOptions Options => new()
    {
        // The .env.example values; set these environment variables if your .env differs.
        Username = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "argus",
        Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "change-me",
    };

    /// <summary>A plain connection, for tests that check the topology itself.</summary>
    public static Task<IConnection> ConnectAsync() => new ConnectionFactory
    {
        HostName = Options.Host,
        UserName = Options.Username,
        Password = Options.Password,
    }.CreateConnectionAsync();

    /// <summary>A started ArgusConnection that declared one client's inbox, like the SDK does.</summary>
    public static async Task<ArgusConnection> StartArgusConnectionAsync(string clientId, RabbitMqOptions? options = null)
    {
        var connection = new ArgusConnection(options ?? Options, clientId,
            (channel, ct) => ArgusTopology.DeclareClientInboxAsync(channel, clientId, ct),
            NullLogger<ArgusConnection>.Instance);
        await connection.StartAsync(CancellationToken.None);
        return connection;
    }

    /// <summary>
    /// Polls a queue until a message arrives, and takes it.
    /// RabbitMQ routes, requeues and dead-letters in the background, so a message isn't
    /// always there the instant after the step that sent it.
    /// </summary>
    /// <param name="autoAck">false leaves the message unacknowledged, so the test can ack or nack it.</param>
    public static async Task<BasicGetResult> WaitForMessageAsync(IChannel channel, string queue, bool autoAck = true)
    {
        for (var i = 0; i < 100; i++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck);
            if (result is not null) return result;
            await Task.Delay(100);
        }
        throw new TimeoutException($"No message arrived in {queue} within 10 s");
    }
}