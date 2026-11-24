namespace Argus.Web.Services;

/// <summary>
/// Every 10 s, marks clients Down when their heartbeats stop (design draft §4.2).
/// Needed because a crashed app can't announce that it crashed: Argus has to notice the silence.
/// </summary>
public class HealthWatchdog(IServiceScopeFactory scopes, ILogger<HealthWatchdog> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first check runs 10 s after startup, so the health consumer can first catch up on queued heartbeats.
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // A singleton can't hold a DbContext: one scope per check, like the consumer's one scope per message.
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IHealthService>()
                    .MarkSilentClientsDownAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // e.g. the database is briefly unreachable: log it and try again on the next tick instead of stopping the watchdog.
                logger.LogError(ex, "Watchdog check failed; retrying in {Seconds} s", Interval.TotalSeconds);
            }
        }
    }
}