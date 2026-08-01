using ManpowerAllocation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ManpowerAllocation.Web.Health;

/// <summary>
/// Readiness check for the governed application database: the app cannot serve requests without it,
/// so an unreachable database is Unhealthy. The description is intentionally generic — no connection
/// string, server name or exception text is surfaced to the (anonymous) health endpoint.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly ManpowerDbContext _dbContext;

    /// <summary>Initialises the check.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    public DatabaseHealthCheck(ManpowerDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var reachable = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return reachable
                ? HealthCheckResult.Healthy("Application database reachable.")
                : HealthCheckResult.Unhealthy("Application database unreachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The exception is passed to the reporter for logging but never written to the response.
            return HealthCheckResult.Unhealthy("Application database check failed.", ex);
        }
    }
}
