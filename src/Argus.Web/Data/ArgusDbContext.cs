using Argus.Contracts;
using Argus.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Data;

public class ArgusDbContext(DbContextOptions<ArgusDbContext> options) : DbContext(options)
{
    public DbSet<ClientApp> ClientApps => Set<ClientApp>();
    public DbSet<DataPoint> DataPoints => Set<DataPoint>();
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<ErrorEvent> ErrorEvents => Set<ErrorEvent>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums are stored as their name ("Down", not 3): readable in the database and safe to reorder.
        builder.Properties<HealthStatus>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<InsightSeverity>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<IncidentStatus>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<MetricAggregation>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<TimeBucket>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<MetricGroupBy>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<ChartType>().HaveConversion<string>().HaveMaxLength(20);

        builder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ClientApp>(entity =>
        {
            entity.HasIndex(app => app.ClientId).IsUnique();

            // Seeded through the migration, so the clients exist wherever the database is created (local and Docker).
            entity.HasData(
                new ClientApp { Id = 1, ClientId = "agora", Name = "Agora" },
                new ClientApp { Id = 2, ClientId = "demo", Name = "Demo client" });
        });

        // Every query filters by client first, then by name or time.
        builder.Entity<DataPoint>().HasIndex(point => new { point.ClientAppId, point.Name, point.TimestampUtc });
        builder.Entity<MetricDefinition>().HasIndex(metric => new { metric.ClientAppId, metric.Name }).IsUnique();
        builder.Entity<Incident>().HasIndex(incident => new { incident.ClientAppId, incident.Status });
        builder.Entity<ErrorEvent>().HasIndex(error => new { error.ClientAppId, error.OccurredAtUtc });
        builder.Entity<Insight>().HasIndex(insight => new { insight.ClientAppId, insight.CreatedAtUtc });
    }
}