namespace ManpowerAllocation.Application.Attendance;

/// <summary>
/// Holds the most recent attendance-sync result for display on the administration screen.
/// Registered as a singleton and accessed from both the background worker and the UI, so
/// access is guarded. History of changes lives in the audit trail, not here.
/// </summary>
public sealed class AttendanceSyncStatus
{
    private readonly object _gate = new();
    private AttendanceSyncResult? _lastRun;

    /// <summary>The result of the most recent run, or null if none has run since startup.</summary>
    public AttendanceSyncResult? LastRun
    {
        get
        {
            lock (_gate)
            {
                return _lastRun;
            }
        }
        set
        {
            lock (_gate)
            {
                _lastRun = value;
            }
        }
    }
}
