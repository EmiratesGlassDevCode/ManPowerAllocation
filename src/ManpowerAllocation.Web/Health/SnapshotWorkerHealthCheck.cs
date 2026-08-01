using ManpowerAllocation.Application.Snapshots;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ManpowerAllocation.Web.Health;

/// <summary>
/// Readiness check for the daily-snapshot background worker. The worker polls once a minute, so a
/// heartbeat older than a few minutes means it has stopped ticking (Degraded) — the app still
/// serves, but the daily report archive would silently stop filling.
/// </summary>
public sealed class SnapshotWorkerHealthCheck : IHealthCheck
{
    // The worker polls every minute; allow a generous margin before calling it stale.
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    private readonly SnapshotHeartbeat _heartbeat;

    /// <summary>Initialises the check.</summary>
    /// <param name="heartbeat">The worker's shared liveness record.</param>
    public SnapshotWorkerHealthCheck(SnapshotHeartbeat heartbeat) => _heartbeat = heartbeat;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lastPoll = _heartbeat.LastPollUtc;

        if (lastPoll is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded("The snapshot worker has not completed a poll yet."));
        }

        var age = DateTime.UtcNow - lastPoll.Value;
        return Task.FromResult(age <= StaleThreshold
            ? HealthCheckResult.Healthy($"Snapshot worker polled {(int)age.TotalSeconds}s ago.")
            : HealthCheckResult.Degraded($"Snapshot worker last polled {(int)age.TotalMinutes} min ago."));
    }
}
