using Argus.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Argus.Client;

public static class ArgusServiceCollectionExtensions
{
    /// <summary>
    /// Plugs the app into Argus: <c>builder.Services.AddArgus(builder.Configuration.GetSection("Argus"));</c>
    /// Throws at startup if the settings are wrong; a RabbitMQ outage never stops the app (see ArgusConnection).
    /// </summary>
    public static IServiceCollection AddArgus(this IServiceCollection services, IConfigurationSection section)
    {
        var options = section.Get<ArgusOptions>()
            ?? throw new InvalidOperationException($"The '{section.Path}' configuration section is missing.");

        if (!ArgusClientId.IsValid(options.ClientId))
            throw new InvalidOperationException(
                $"{section.Path}:ClientId '{options.ClientId}' is invalid: use 1-50 lowercase letters, digits or '-'.");
        if (string.IsNullOrEmpty(options.RabbitMq.Password))
            throw new InvalidOperationException(
                $"{section.Path}:RabbitMq:Password is missing. Set it in user-secrets (locally) or as an environment variable (Docker).");

        services.AddSingleton(options);
        services.AddSingleton(provider => new ArgusConnection(
            options.RabbitMq,
            connectionName: options.ClientId,
            declareTopology: async (channel, ct) =>
            {
                await ArgusTopology.DeclareIngestAsync(channel, ct);                       // where this client sends to
                await ArgusTopology.DeclareClientInboxAsync(channel, options.ClientId, ct); // where Argus replies
            },
            provider.GetRequiredService<ILogger<ArgusConnection>>()));
        services.AddHostedService(provider => provider.GetRequiredService<ArgusConnection>());

        return services;
    }
}