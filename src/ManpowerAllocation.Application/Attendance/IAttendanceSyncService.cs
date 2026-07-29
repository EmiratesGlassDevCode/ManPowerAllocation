namespace ManpowerAllocation.Application.Attendance;

/// <summary>
/// Synchronises stored employee attendance from the external attendance view. Invoked on a
/// schedule by a background worker and on demand from the administration screen.
/// </summary>
public interface IAttendanceSyncService
{
    /// <summary>
    /// Reads today's check-ins and updates each non-outsource employee's status: a check-in means
    /// Present (overriding a prior vacation, since punching in means the person has resumed);
    /// otherwise a supervisor-set vacation is preserved; otherwise the employee is Absent. The run
    /// is recorded as a single summary entry in the audit trail.
    /// </summary>
    /// <param name="triggeredBy">A short, non-sensitive description of what triggered the run (e.g. "scheduled" or "manual").</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<AttendanceSyncResult> SyncAsync(string triggeredBy, CancellationToken cancellationToken = default);
}
