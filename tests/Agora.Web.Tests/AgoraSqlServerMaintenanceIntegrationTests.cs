using Agora.Web.Data;
using Agora.Web.Services;
using Argus.Client;
using Argus.Contracts.Outbound;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Sdk;

namespace Agora.Web.Tests;

/// <summary>
/// Opt-in regression coverage for the SQL Server IDENTITY behavior that SQLite cannot emulate.
/// Set AGORA_SQLSERVER_INTEGRATION_CONNECTION to a server-level test connection; this test creates
/// and drops only a uniquely named AgoraAA46Identity_* database, never AgoraDb.
/// </summary>
public sealed class AgoraSqlServerMaintenanceIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Forward_migration_initializes_identity_singleton_and_maintenance_messages_remain_idempotent()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("AGORA_SQLSERVER_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
            throw SkipException.ForSkip("Set AGORA_SQLSERVER_INTEGRATION_CONNECTION to run isolated SQL Server AA-46 integration coverage.");

        var databaseName = $"AgoraAA46Identity_{Guid.NewGuid():N}";
        var testBuilder = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = databaseName };
        var masterBuilder = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = "master" };
        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync();
        await using (var create = master.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<AgoraDbContext>().UseSqlServer(testBuilder.ConnectionString).Options;
            await using (var db = new AgoraDbContext(options))
            {
                await db.Database.MigrateAsync();
                var singleton = await db.MaintenanceStates.SingleAsync();
                Assert.Equal(1, singleton.Id);
                Assert.False(singleton.IsEnabled);

                var notifier = new Notifier();
                var handler = new AgoraArgusMessageHandler(db, new ArgusOptions { ClientId = "agora" }, notifier, NullLogger<AgoraArgusMessageHandler>.Instance);
                var on = new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = DateTimeOffset.UtcNow, Enabled = true };
                await handler.HandleMaintenanceModeChangedAsync(on);
                await handler.HandleMaintenanceModeChangedAsync(on);
                await handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = on.OccurredAt.AddMinutes(1), Enabled = false });
                await handler.HandleMaintenanceModeChangedAsync(new MaintenanceModeChanged { MessageId = Guid.NewGuid(), ClientId = "agora", OccurredAt = on.OccurredAt.AddMinutes(-1), Enabled = true });
                Assert.Equal(2, notifier.Broadcasts);
            }

            await using var reloaded = new AgoraDbContext(options);
            Assert.False((await reloaded.MaintenanceStates.SingleAsync(item => item.Id == 1)).IsEnabled);
        }
        finally
        {
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class Notifier : IPublicMaintenanceNotifier
    {
        public int Broadcasts { get; private set; }
        public Task StatusChangedAsync(MaintenanceStatus status) { Broadcasts++; return Task.CompletedTask; }
    }
}
