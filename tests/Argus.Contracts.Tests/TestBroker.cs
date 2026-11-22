using RabbitMQ.Client;

namespace Argus.Contracts.Tests;

/// <summary>Shared helpers for the integration tests that talk to RabbitMQ on localhost:5672.</summary>
internal static class TestBroker
{
    public static Task<IConnection> ConnectAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            // The .env.example values; set these environment variables if your .env differs.
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "argus",
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "change-me",
        };
        return factory.CreateConnectionAsync();
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