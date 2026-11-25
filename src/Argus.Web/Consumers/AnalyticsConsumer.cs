using Argus.Contracts;
using Argus.Web.Services;

namespace Argus.Web.Consumers;

/// <summary>
/// Consumes argus.analytics. Its own queue, so a burst of events can never delay heartbeats (and Down detection);
/// the processing rules are the same as for health messages, so it hands each message to the same HealthService.
/// </summary>
public class AnalyticsConsumer(ArgusConnection connection, IServiceScopeFactory scopes, ILogger<AnalyticsConsumer> logger)
    : ArgusConsumer(connection, logger)
{
    protected override string QueueName => ArgusQueues.Analytics;

    protected override async Task HandleAsync(ArgusMessage message, CancellationToken ct)
    {
        // One scope (and DbContext) per message, like the health consumer.
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IHealthService>().ProcessAsync(message, ct);
    }
}