namespace ManpowerAllocation.Application.Email;

/// <summary>
/// Holds the most recent daily-report email outcome for display on the admin screen. Registered as
/// a singleton and written by the background worker; access is guarded. The full history lives in
/// the audit/log trail, not here.
/// </summary>
public sealed class EmailSendStatus
{
    private readonly object _gate = new();
    private DateTime? _lastPollUtc;
    private DateTime? _lastSuccessUtc;
    private DateTime? _lastSentOperationalDate;
    private DateTime? _lastFailureUtc;
    private string? _lastError;

    /// <summary>When the worker last completed a poll cycle, or null before the first poll.</summary>
    public DateTime? LastPollUtc
    {
        get { lock (_gate) { return _lastPollUtc; } }
    }

    /// <summary>When the daily report was last emailed successfully, or null if never this run.</summary>
    public DateTime? LastSuccessUtc
    {
        get { lock (_gate) { return _lastSuccessUtc; } }
    }

    /// <summary>The operational date of the last successfully emailed report, if any.</summary>
    public DateTime? LastSentOperationalDate
    {
        get { lock (_gate) { return _lastSentOperationalDate; } }
    }

    /// <summary>When the last send attempt failed, or null if none has failed this run.</summary>
    public DateTime? LastFailureUtc
    {
        get { lock (_gate) { return _lastFailureUtc; } }
    }

    /// <summary>A short, non-sensitive description of the last failure, if any.</summary>
    public string? LastError
    {
        get { lock (_gate) { return _lastError; } }
    }

    /// <summary>Records that the worker completed a poll at the given instant.</summary>
    /// <param name="utcNow">The poll completion time (UTC).</param>
    public void MarkPoll(DateTime utcNow)
    {
        lock (_gate) { _lastPollUtc = utcNow; }
    }

    /// <summary>Records a successful send.</summary>
    /// <param name="utcNow">The send time (UTC).</param>
    /// <param name="operationalDate">The operational date that was emailed.</param>
    public void MarkSuccess(DateTime utcNow, DateTime operationalDate)
    {
        lock (_gate)
        {
            _lastSuccessUtc = utcNow;
            _lastSentOperationalDate = operationalDate;
            _lastError = null;
        }
    }

    /// <summary>Records a failed send attempt.</summary>
    /// <param name="utcNow">The failure time (UTC).</param>
    /// <param name="error">A short, non-sensitive error description.</param>
    public void MarkFailure(DateTime utcNow, string error)
    {
        lock (_gate)
        {
            _lastFailureUtc = utcNow;
            _lastError = error;
        }
    }
}
