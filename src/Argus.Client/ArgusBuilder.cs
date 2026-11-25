using Microsoft.Extensions.DependencyInjection;

namespace Argus.Client;

/// <summary>A gauge: a current value read on every heartbeat, e.g. "reports.pending" → 4.</summary>
public sealed record ArgusGauge(string Name, Func<IServiceProvider, Task<decimal>> Read);

/// <summary>Returned by AddArgus, so gauges can be chained: <c>AddArgus(...).AddGauge(...)</c>.</summary>
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
}