using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Snapshots;

/// <summary>
/// Records the liveness of the daily-snapshot background worker for health reporting and the UI.
/// Registered as a singleton and written by the worker on every poll; read by the health endpoint
/// and the navigation footer. A stale <see cref="LastPollUtc"/> means the worker has stopped ticking.
/// </summary>
public sealed class SnapshotHeartbeat
{
    private readonly object _gate = new();
    private DateTime? _lastPollUtc;
    private DateTime? _lastCaptureUtc;
    private DateTime? _lastCaptureDate;
    private ShiftType? _lastCaptureShift;

    /// <summary>When the worker last completed a poll cycle, or null before the first poll.</summary>
    public DateTime? LastPollUtc
    {
        get { lock (_gate) { return _lastPollUtc; } }
    }

    /// <summary>When a snapshot was last captured, or null if none has been captured this run.</summary>
    public DateTime? LastCaptureUtc
    {
        get { lock (_gate) { return _lastCaptureUtc; } }
    }

    /// <summary>The operational date of the last captured snapshot, if any.</summary>
    public DateTime? LastCaptureDate
    {
        get { lock (_gate) { return _lastCaptureDate; } }
    }

    /// <summary>The shift of the last captured snapshot, if any.</summary>
    public ShiftType? LastCaptureShift
    {
        get { lock (_gate) { return _lastCaptureShift; } }
    }

    /// <summary>Records that the worker completed a poll at the given instant.</summary>
    /// <param name="utcNow">The poll completion time (UTC).</param>
    public void MarkPoll(DateTime utcNow)
    {
        lock (_gate) { _lastPollUtc = utcNow; }
    }

    /// <summary>Records that a snapshot was captured.</summary>
    /// <param name="utcNow">The capture time (UTC).</param>
    /// <param name="operationalDate">The captured operational date.</param>
    /// <param name="shift">The captured shift.</param>
    public void MarkCapture(DateTime utcNow, DateTime operationalDate, ShiftType shift)
    {
        lock (_gate)
        {
            _lastCaptureUtc = utcNow;
            _lastCaptureDate = operationalDate;
            _lastCaptureShift = shift;
        }
    }
}
