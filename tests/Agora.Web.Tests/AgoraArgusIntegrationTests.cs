using Agora.Web.Data;
using Agora.Web.Services;
using Argus.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Agora.Web.Tests;

public class AgoraArgusIntegrationTests
{
    [Fact]
    public void Argus_configuration_registers_the_sdk_with_Agora_client_identity_and_heartbeat_interval()
    {
        var configuration = ValidConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddArgus(configuration.GetSection("Argus"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ArgusOptions>();
        Assert.Equal("agora", options.ClientId);
        Assert.Equal(10, options.HeartbeatIntervalSeconds);
        Assert.Equal("127.0.0.1", options.RabbitMq.Host);
        Assert.Equal(5672, options.RabbitMq.Port);
        Assert.Equal("argus", options.RabbitMq.Username);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RequestMetrics));
        Assert.True(services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)) >= 2);
    }

    [Fact]
    public void Missing_Argus_broker_password_fails_with_a_safe_configuration_message()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Argus:ClientId"] = "agora",
            ["Argus:HeartbeatIntervalSeconds"] = "10",
            ["Argus:RabbitMq:Username"] = "argus"
        }).Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddArgus(configuration.GetSection("Argus")));

        Assert.Contains("Argus:RabbitMq:Password is missing", exception.Message);
    }

    [Fact]
    public async Task Registered_Agora_database_health_check_reports_healthy_for_a_reachable_relational_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
        services.AddHealthChecks().AddCheck<AgoraDatabaseHealthCheck>("AgoraDb");
        await using var provider = services.BuildServiceProvider();

        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var report = await healthChecks.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains("AgoraDb", report.Entries.Keys);
    }

    private static IConfiguration ValidConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Argus:ClientId"] = "agora",
        ["Argus:HeartbeatIntervalSeconds"] = "10",
        ["Argus:RabbitMq:Host"] = "127.0.0.1",
        ["Argus:RabbitMq:Port"] = "5672",
        ["Argus:RabbitMq:VirtualHost"] = "/",
        ["Argus:RabbitMq:Username"] = "argus",
        ["Argus:RabbitMq:Password"] = "test-secret"
    }).Build();
}
