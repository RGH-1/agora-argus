using Argus.Contracts;
using Argus.Contracts.Outbound;
using Microsoft.Extensions.DependencyInjection;

namespace Argus.Client;

/// <summary>A gauge: a current value read on every heartbeat, e.g. "reports.pending" → 4.</summary>
public sealed record ArgusGauge(string Name, Func<IServiceProvider, Task<decimal>> Read);

/// <summary>What the client does when a message of MessageType arrives from Argus in its inbox.</summary>
public sealed record ArgusHandler(Type MessageType, Func<IServiceProvider, ArgusMessage, Task> Handle);

/// <summary>Returned by AddArgus, so gauges and inbox handlers can be chained: <c>AddArgus(...).AddGauge(...).OnMaintenanceModeChanged(...)</c>.</summary>
public sealed class ArgusBuilder(IServiceCollection services)
{
    private readonly HashSet<string> _gaugeNames = [];

    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Adds a gauge. <typeparamref name="TService"/> comes from a fresh DI scope on every heartbeat, so a DbContext is fine:
    /// <code>.AddGauge("reports.pending", async (AgoraDbContext db) => await db.Reports.CountAsync(r => r.Status == ReportStatus.Pending))</code>
    /// </summary>
    public ArgusBuilder AddGauge<TService>(string name, Func<TService, Task<decimal>> read) where TService : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100) throw new ArgumentException("A gauge name can be at most 100 characters.", nameof(name));
        if (name.StartsWith("sys.")) throw new ArgumentException("Gauge names starting with 'sys.' are reserved for the SDK's own numbers.", nameof(name));
        if (!_gaugeNames.Add(name)) throw new ArgumentException($"The gauge '{name}' was added twice.", nameof(name));

        Services.AddSingleton(new ArgusGauge(name, provider => read(provider.GetRequiredService<TService>())));
        return this;
    }

    // Handlers for messages from Argus. TService comes from a fresh DI scope per message, so a DbContext is fine.
    // RabbitMQ delivers at least once, so a handler can (rarely) see the same message twice: use MessageId to skip repeats.

    /// <summary><code>.OnMaintenanceModeChanged((MaintenanceState state, MaintenanceModeChanged message) => state.SetAsync(message))</code></summary>
    public ArgusBuilder OnMaintenanceModeChanged<TService>(Func<TService, MaintenanceModeChanged, Task> handle) where TService : notnull =>
        On(handle);

    public ArgusBuilder OnHealthStatusChanged<TService>(Func<TService, HealthStatusChanged, Task> handle) where TService : notnull =>
        On(handle);

    public ArgusBuilder OnInsightPublished<TService>(Func<TService, InsightPublished, Task> handle) where TService : notnull =>
        On(handle);

    private ArgusBuilder On<TService, TMessage>(Func<TService, TMessage, Task> handle)
        where TService : notnull where TMessage : ArgusMessage
    {
        Services.AddSingleton(new ArgusHandler(typeof(TMessage),
            (provider, message) => handle(provider.GetRequiredService<TService>(), (TMessage)message)));
        return this;
    }
}