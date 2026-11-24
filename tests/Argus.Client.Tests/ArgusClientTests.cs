using System.Text.Json;
using Argus.Contracts;
using Argus.Contracts.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Argus.Client.Tests;

/// <summary>
/// Runs a small in-memory web app that uses the SDK like Agora does, and listens to what it sends.
/// Needs RabbitMQ on 127.0.0.1:5672. The listener is a private copy of argus.ingest's health traffic,
/// so it never takes messages away from argus.health.
/// </summary>
[Trait("Category", "Integration")]
public class ArgusClientTests
{
    private const string ClientId = "sdk-test";

    [Fact]
    public async Task Client_announces_itself_sends_heartbeats_reports_errors_and_says_goodbye()
    {
        await using var connection = await new ConnectionFactory
        {
            HostName = "127.0.0.1",
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "argus",
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "change-me",
        }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await ArgusTopology.DeclareIngestAsync(channel);
        var listener = (await channel.QueueDeclareAsync(exclusive: true)).QueueName; // deleted when the test ends
        await channel.QueueBindAsync(listener, ArgusExchanges.Ingest, ArgusRoutingKeys.AllHealth);

        var app = BuildApp();
        await app.StartAsync();
        try
        {
            var started = await WaitForAsync<ClientStarted>(channel, listener);
            Assert.Equal("Development", started.Environment);

            var http = app.GetTestClient();
            Assert.Equal("ok", await http.GetStringAsync("/"));
            var failing = await http.GetAsync("/boom");
            Assert.Equal("our error page", await failing.Content.ReadAsStringAsync()); // the app's error page is unchanged

            var error = await WaitForAsync<ErrorReported>(channel, listener);
            Assert.Equal("/boom", error.Path);
            Assert.Equal(typeof(InvalidOperationException).FullName, error.ExceptionType);

            // Heartbeats every second here; together they report both requests, one of them failed.
            var (requests, errors) = (0, 0);
            while (requests < 2)
            {
                var heartbeat = await WaitForAsync<Heartbeat>(channel, listener, expiration: "30000");
                (requests, errors) = (requests + heartbeat.Requests, errors + heartbeat.Errors);
                Assert.True(heartbeat.DbHealthy);
                Assert.NotNull(heartbeat.DbResponseMs);
                Assert.True(heartbeat.MemoryMb > 0);
            }
            Assert.Equal((2, 1), (requests, errors));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        await WaitForAsync<ClientStopping>(channel, listener);
        await channel.QueueDeleteAsync(ArgusQueues.Inbox(ClientId));
        await channel.QueueDeleteAsync(ArgusQueues.ClientDead(ClientId));
    }

    private static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Argus:ClientId"] = ClientId,
            ["Argus:HeartbeatIntervalSeconds"] = "1",
            ["Argus:RabbitMq:Username"] = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "argus",
            ["Argus:RabbitMq:Password"] = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "change-me",
        });
        builder.Services.AddArgus(builder.Configuration.GetSection("Argus"));
        builder.Services.AddHealthChecks().AddCheck("db", () => HealthCheckResult.Healthy());

        var app = builder.Build();
        app.UseExceptionHandler(errorApp => errorApp.Run(context => context.Response.WriteAsync("our error page")));
        app.UseArgusMetrics();
        app.MapGet("/", () => "ok");
        app.MapGet("/boom", string () => throw new InvalidOperationException("Simulated failure"));
        return app;
    }

    /// <summary>Reads the listener until a message of type T from this test's client arrives (other clients' traffic is skipped).</summary>
    private static async Task<T> WaitForAsync<T>(IChannel channel, string queue, string? expiration = null) where T : ArgusMessage
    {
        for (var i = 0; i < 150; i++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: true);
            if (result is null)
            {
                await Task.Delay(100);
                continue;
            }
            if (result.BasicProperties.Type != typeof(T).Name) continue;

            var message = JsonSerializer.Deserialize<T>(result.Body.Span, ArgusJson.Options)!;
            if (message.ClientId != ClientId) continue;

            if (expiration is not null) Assert.Equal(expiration, result.BasicProperties.Expiration);
            return message;
        }
        throw new TimeoutException($"No {typeof(T).Name} from {ClientId} within 15 s");
    }
}