
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Agora.Web.Data;

public class AgoraDbContextFactory
    : IDesignTimeDbContextFactory<AgoraDbContext>
{
    public AgoraDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<AgoraDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("AgoraDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "AgoraDb connection string is missing. " +
                "Configure ConnectionStrings:AgoraDb in User Secrets.");
        }

        var options = new DbContextOptionsBuilder<AgoraDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AgoraDbContext(options);
    }
}
