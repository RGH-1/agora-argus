using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Argus.Contracts;

/// <summary>
/// The one RabbitMQ connection of an app (Argus or a client), shared by its publisher and consumers.
/// It runs in the background, so a broker that is down never stops the app from starting:
/// it retries until RabbitMQ answers, declares the topology, and from then on
/// RabbitMQ.Client's automatic recovery reconnects (channels and consumers included) after an outage.
/// </summary>
public sealed class ArgusConnection(
    RabbitMqOptions options,
    string connectionName,
    Func<IChannel, CancellationToken, Task> declareTopology,
    ILogger<ArgusConnection> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly TaskCompletionSource<IConnection> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The connection once it has opened, or null while RabbitMQ hasn't been reached yet.</summary>
    public IConnection? Current => _connected.Task.IsCompletedSuccessfully ? _connected.Task.Result : null;

    /// <summary>Waits until the connection has opened (used by consumers, which can simply wait).</summary>
    public Task<IConnection> GetAsync(CancellationToken ct) => _connected.Task.WaitAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.Username,
            Password = options.Password,
            ClientProvidedName = connectionName,  // the name shown in the Management UI's Connections tab
            AutomaticRecoveryEnabled = true,      // after an outage: reconnect, reopen channels, restart consumers
            NetworkRecoveryInterval = RetryDelay,
        };

        // Automatic recovery only covers a connection that opened once, so the first connect is retried here.
        while (true)
        {
            IConnection? connection = null;
            try
            {
                connection = await factory.CreateConnectionAsync(stoppingToken);
                await using (var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken))
                {
                    await declareTopology(channel, stoppingToken);
                }

                connection.ConnectionShutdownAsync += OnShutdownAsync;
                connection.RecoverySucceededAsync += OnRecoveredAsync;
                _connected.SetResult(connection);
                logger.LogInformation("Connected to RabbitMQ at {Host}:{Port} as {Name}", options.Host, options.Port, connectionName);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                if (connection is not null) await connection.DisposeAsync();
                logger.LogError("RabbitMQ at {Host}:{Port} is unreachable ({Error}); retrying in {Delay} s",
                    options.Host, options.Port, ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (Current is { } connection) await connection.CloseAsync(cancellationToken);
    }

    private Task OnShutdownAsync(object sender, ShutdownEventArgs args)
    {
        // Our own CloseAsync on app stop isn't an error.
        if (args.Initiator != ShutdownInitiator.Application)
            logger.LogError("Lost the RabbitMQ connection ({Reason}); reconnecting automatically", args.ReplyText);
        return Task.CompletedTask;
    }

    private Task OnRecoveredAsync(object sender, AsyncEventArgs args)
    {
        logger.LogInformation("Reconnected to RabbitMQ");
        return Task.CompletedTask;
    }
}