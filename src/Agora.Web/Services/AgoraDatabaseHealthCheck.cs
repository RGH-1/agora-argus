using Agora.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agora.Web.Services;

public sealed class AgoraDatabaseHealthCheck(AgoraDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Agora database is reachable.")
                : HealthCheckResult.Unhealthy("Agora database is unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Agora database is unavailable.");
        }
    }
}
