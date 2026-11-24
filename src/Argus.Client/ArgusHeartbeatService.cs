using System.Diagnostics;
using System.Reflection;
using Argus.Contracts;
using Argus.Contracts.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using HealthCheckStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus; // not Argus's HealthStatus

namespace Argus.Client;

/// <summary>
/// Announces the client (ClientStarted), sends a Heartbeat every HeartbeatIntervalSeconds,
/// and says goodbye (ClientStopping) on a clean shutdown, so Argus can tell "stopped" from "crashed".
/// </summary>
public sealed class ArgusHeartbeatService(
    ArgusOptions options,
    ArgusConnection connection,
    ArgusSender sender,
    RequestMetrics metrics,
    IHostEnvironment environment,
    IServiceProvider services) : BackgroundService
{
    /// <summary>A heartbeat older than this is worthless: RabbitMQ drops it instead of keeping it while Argus is down.</summary>
    public static readonly TimeSpan HeartbeatExpiration = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // "Started" is only useful once the broker can take it, so wait for the connection first.
        await connection.GetAsync(stoppingToken);
        await sender.SendAsync(ArgusRoutingKeys.ClientStarted, new ClientStarted
        {
            Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            Environment = environment.EnvironmentName,
            MachineName = Environment.MachineName,
        });

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.HeartbeatIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await sender.SendAsync(ArgusRoutingKeys.Heartbeat, await BuildHeartbeatAsync(stoppingToken), HeartbeatExpiration);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await sender.SendAsync(ArgusRoutingKeys.ClientStopping, new ClientStopping { Reason = "Application stopping" });
    }

    private async Task<Heartbeat> BuildHeartbeatAsync(CancellationToken ct)
    {
        var (requests, errors, avgResponseMs) = metrics.TakeAndReset();
        var heartbeat = new Heartbeat
        {
            MemoryMb = Math.Round(Environment.WorkingSet / 1024.0 / 1024.0, 1),
            UptimeSec = (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds,
            Requests = requests,
            Errors = errors,
            AvgResponseMs = Math.Round(avgResponseMs, 1),
        };

        // The client's ASP.NET Core health checks (e.g. AddDbContextCheck) decide DbHealthy.
        // A client without health checks has no database to report, so both stay null.
        if (services.GetService<HealthCheckService>() is { } healthChecks)
        {
            var report = await healthChecks.CheckHealthAsync(ct);
            heartbeat.DbHealthy = report.Status == HealthCheckStatus.Healthy;
            heartbeat.DbResponseMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1);
        }

        return heartbeat;
    }
}