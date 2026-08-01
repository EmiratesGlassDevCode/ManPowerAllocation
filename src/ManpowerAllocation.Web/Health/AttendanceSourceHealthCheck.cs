using ManpowerAllocation.Application.Attendance;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ManpowerAllocation.Web.Health;

/// <summary>
/// Readiness check for the biometric attendance feed. A failed or not-yet-run sync is reported as
/// Degraded — not Unhealthy — because the dashboards keep working off the last-known state when the
/// external source is briefly unavailable; the app itself is still serving.
/// </summary>
public sealed class AttendanceSourceHealthCheck : IHealthCheck
{
    private readonly AttendanceSyncStatus _status;

    /// <summary>Initialises the check.</summary>
    /// <param name="status">The shared last-sync result holder.</param>
    public AttendanceSourceHealthCheck(AttendanceSyncStatus status) => _status = status;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var run = _status.LastRun;

        if (run is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded("No attendance sync has run since startup."));
        }

        return Task.FromResult(run.Success
            ? HealthCheckResult.Healthy($"Last attendance sync succeeded at {run.RanAtUtc:yyyy-MM-dd HH:mm} UTC.")
            : HealthCheckResult.Degraded("The most recent attendance sync did not complete."));
    }
}
