using Argus.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Argus.Client;

/// <summary>
/// Consumes this client's inbox ({clientId}.inbox) and passes each message from Argus to the handlers registered with
/// <c>AddArgus(...).OnMaintenanceModeChanged(...)</c> and friends. The base class does the RabbitMQ part:
/// a handler that throws is retried, and after 3 failures the message goes to {clientId}.dead.
/// </summary>
public sealed class ArgusInboxConsumer(
    ArgusOptions options,
    ArgusConnection connection,
    IEnumerable<ArgusHandler> handlers,
    IServiceScopeFactory scopes,
    ILogger<ArgusInboxConsumer> logger) : ArgusConsumer(connection, logger)
{
    private readonly ArgusHandler[] _handlers = handlers.ToArray();

    protected override string QueueName => ArgusQueues.Inbox(options.ClientId);

    protected override async Task HandleAsync(ArgusMessage message, CancellationToken ct)
    {
        // A message type the client didn't register a handler for is simply acked: nobody here needs it.
        var matching = _handlers.Where(handler => handler.MessageType == message.GetType()).ToArray();
        if (matching.Length == 0) return;

        // Handlers may use scoped services such as a DbContext: one scope per message, like Argus's own consumers.
        await using var scope = scopes.CreateAsyncScope();
        foreach (var handler in matching) await handler.Handle(scope.ServiceProvider, message);
    }
}