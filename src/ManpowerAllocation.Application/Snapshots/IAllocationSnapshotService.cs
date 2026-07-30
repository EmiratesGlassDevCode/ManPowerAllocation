using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Snapshots;

/// <summary>
/// Captures and persists the daily staffing report as an immutable snapshot for one operational
/// date and shift. Snapshots accumulate over time to form the history that the analytics views
/// query.
/// </summary>
public interface IAllocationSnapshotService
{
    /// <summary>Returns true when a snapshot already exists for the given operational date and shift.</summary>
    /// <param name="operationalDate">The operational (calendar) date.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<bool> ExistsAsync(DateTime operationalDate, ShiftType shift, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the current staffing report for the given shift and operational date and stores it,
    /// replacing any existing snapshot for the same date and shift. The department figures reuse the
    /// live dashboard calculation, so a snapshot equals what the report screen showed at capture time.
    /// </summary>
    /// <param name="shift">The shift to capture.</param>
    /// <param name="operationalDate">The operational (calendar) date to stamp the snapshot with.</param>
    /// <param name="source">How the capture was triggered (e.g. "auto-10:00").</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task CaptureAsync(ShiftType shift, DateTime operationalDate, string source, CancellationToken cancellationToken = default);
}
