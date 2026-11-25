using Argus.Contracts;
using Argus.Contracts.Health;
using Argus.Web.Data.Entities;
using Argus.Web.Repositories;

namespace Argus.Web.Services;

/// <summary>What Argus does with each health message (design draft §4.2 and §7.2).</summary>
public class HealthService(
    IClientAppRepository clientApps,
    IHealthRepository health,
    IIncidentRepository incidents,
    ILogger<HealthService> logger) : IHealthService
{
    public async Task ProcessAsync(ArgusMessage message, CancellationToken cancellationToken = default)
    {
        // RabbitMQ delivers at least once, so the same message can arrive twice: skip it the second time.
        if (await health.IsProcessedAsync(message.MessageId, cancellationToken))
        {
            logger.LogInformation("Skipped duplicate {Type} {MessageId}", message.GetType().Name, message.MessageId);
            return;
        }

        // Tracked by the same DbContext as the health repository, so SaveAsync below also saves these changes.
        var client = await clientApps.GetByClientIdAsync(message.ClientId, cancellationToken);
        if (client is null || client.IsSuspended)
        {
            logger.LogWarning("Ignored {Type} from {Reason} client '{ClientId}'",
                message.GetType().Name, client is null ? "unknown" : "suspended", message.ClientId);
            return;
        }

        switch (message)
        {
            case ClientStarted started:
                client.Version = Cut(started.Version, 50);
                client.LastHeartbeatAtUtc = DateTime.UtcNow; // counts as a sign of life, so the watchdog waits for the first heartbeat
                await SetHealthAsync(client, HealthStatus.Healthy, null, cancellationToken);
                break;

            case Heartbeat heartbeat:
                client.LastHeartbeatAtUtc = DateTime.UtcNow; // when Argus heard it, so a client's wrong clock can't fool the watchdog
                health.AddDataPoints(ToDataPoints(client, heartbeat));
                var problem = FindProblem(client, heartbeat);
                await SetHealthAsync(client, problem is null ? HealthStatus.Healthy : HealthStatus.Degraded, problem, cancellationToken);
                break;

            case ClientStopping:
                // A planned shutdown: no incident, and the watchdog must not call it Down.
                await SetHealthAsync(client, HealthStatus.Stopped, null, cancellationToken);
                break;

            case ErrorReported error:
                health.AddError(new ErrorEvent
                {
                    ClientApp = client,
                    Message = Cut(error.Message, 2000),
                    ExceptionType = Cut(error.ExceptionType, 300),
                    Path = error.Path is null ? null : Cut(error.Path, 500),
                    OccurredAtUtc = error.OccurredAt.UtcDateTime,
                });
                break;

            default:
                logger.LogWarning("Ignored unexpected {Type} on the health queue", message.GetType().Name);
                return;
        }

        // Processing and remembering the MessageId are saved together: either both happen or neither does.
        health.MarkProcessed(message.MessageId);
        await health.SaveAsync(cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, decimal>> GetLatestHeartbeatAsync(int clientAppId, CancellationToken cancellationToken = default) =>
        health.GetLatestHeartbeatAsync(clientAppId, cancellationToken);

    public async Task MarkSilentClientsDownAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Stopped clients aren't loaded: they said goodbye, so their silence is expected (§4.2).
        var silent = (await health.GetLiveClientsAsync(cancellationToken))
            .Where(client => nowUtc - client.LastHeartbeatAtUtc > TimeSpan.FromSeconds(client.HeartbeatTimeoutSec))
            .ToList();
        if (silent.Count == 0) return;

        foreach (var client in silent)
        {
            // A crashed app can't say it crashed: no heartbeat and no ClientStopping means Down.
            // shortcut: a heartbeat saved between the load above and SaveAsync is overwritten until the next one (≤10 s); add a concurrency token if that matters.
            await SetHealthAsync(client, HealthStatus.Down, $"No heartbeat for over {client.HeartbeatTimeoutSec} s", cancellationToken);
        }
        await health.SaveAsync(cancellationToken);
    }

    /// <summary>
    /// Every health change goes through here, so incidents open and close in one place (§4.2):
    /// Down or Degraded opens one, Healthy closes it, Stopped never opens one.
    /// The caller saves, so the change and its incident are stored together.
    /// </summary>
    private async Task SetHealthAsync(ClientApp client, HealthStatus status, string? reason, CancellationToken cancellationToken)
    {
        if (client.Health == status) return;
        logger.LogWarning("Client '{ClientId}' is {Status} (was {Previous}){Reason}",
            client.ClientId, status, client.Health, reason is null ? "" : $": {reason}");
        client.Health = status;

        if (status is HealthStatus.Down or HealthStatus.Degraded)
        {
            // One incident per outage: Degraded → Down keeps the incident that's already open.
            if (await incidents.GetOpenAsync(client.Id, cancellationToken) is null)
            {
                incidents.Add(new Incident
                {
                    ClientApp = client,
                    Cause = status,
                    Reason = Cut(reason ?? status.ToString(), 500),
                    OpenedAtUtc = DateTime.UtcNow,
                });
            }
        }
        else if (status == HealthStatus.Healthy && await incidents.GetOpenAsync(client.Id, cancellationToken) is { } open)
        {
            open.Status = IncidentStatus.Resolved; // recovered on its own
            open.ResolvedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>Why a live client is Degraded, or null when the heartbeat is within its thresholds (§4.1).</summary>
    private static string? FindProblem(ClientApp client, Heartbeat heartbeat)
    {
        if (heartbeat.DbHealthy == false) return "Database check failed";
        if (heartbeat.DbResponseMs > client.SlowDbMs)
            return $"Slow database: {heartbeat.DbResponseMs:0} ms (limit {client.SlowDbMs} ms)";

        // Requests and errors cover only the interval since the previous heartbeat.
        if (heartbeat.Requests > 0)
        {
            var errorRate = heartbeat.Errors * 100.0 / heartbeat.Requests;
            if (errorRate > client.MaxErrorRatePercent)
                return $"Error rate {errorRate:0}% ({heartbeat.Errors} of {heartbeat.Requests} requests, limit {client.MaxErrorRatePercent}%)";
        }
        return null;
    }

    /// <summary>Every heartbeat number becomes a data point, so the metric builder can chart it (§4.3).</summary>
    private static IEnumerable<DataPoint> ToDataPoints(ClientApp client, Heartbeat heartbeat)
    {
        var values = new Dictionary<string, decimal>
        {
            ["sys.requests"] = heartbeat.Requests,
            ["sys.errors"] = heartbeat.Errors,
            ["sys.avg_response_ms"] = (decimal)heartbeat.AvgResponseMs,
            ["sys.memory_mb"] = (decimal)heartbeat.MemoryMb,
            ["sys.uptime_sec"] = heartbeat.UptimeSec,
        };
        if (heartbeat.DbHealthy is { } dbHealthy) values["sys.db_healthy"] = dbHealthy ? 1 : 0;
        if (heartbeat.DbResponseMs is { } dbResponseMs) values["sys.db_response_ms"] = (decimal)dbResponseMs;
        foreach (var (name, value) in heartbeat.Gauges) values[Cut(name, 100)] = value;

        return values.Select(pair => new DataPoint
        {
            ClientApp = client,
            Name = pair.Key,
            Value = pair.Value,
            TimestampUtc = heartbeat.OccurredAt.UtcDateTime,
        });
    }

    // Columns have a max length; a client could send longer text.
    private static string Cut(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}