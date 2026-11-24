using Argus.Contracts;
using Argus.Web.Services;

namespace Argus.Web.Consumers;

/// <summary>
/// Consumes argus.health. The base class does the RabbitMQ part (ack, retries, dead-lettering);
/// this class only hands each message to the HealthService.
/// </summary>
public class HealthConsumer(ArgusConnection connection, IServiceScopeFactory scopes, ILogger<HealthConsumer> logger)
    : ArgusConsumer(connection, logger)
{
    protected override string QueueName => ArgusQueues.Health;

    protected override async Task HandleAsync(ArgusMessage message, CancellationToken ct)
    {
        // The consumer lives for the whole app, but a DbContext must be short-lived: one scope per message.
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IHealthService>().ProcessAsync(message, ct);
    }
}