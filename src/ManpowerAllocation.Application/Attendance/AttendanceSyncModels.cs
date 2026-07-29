namespace ManpowerAllocation.Application.Attendance;

/// <summary>The outcome of a single attendance synchronisation run.</summary>
public sealed record AttendanceSyncResult
{
    /// <summary>UTC time the run completed.</summary>
    public DateTime RanAtUtc { get; init; }

    /// <summary>Whether the run succeeded (a source being unconfigured or unreachable is a failure).</summary>
    public bool Success { get; init; }

    /// <summary>Number of employees resolved to Present.</summary>
    public int Present { get; init; }

    /// <summary>Number of employees resolved to Absent.</summary>
    public int Absent { get; init; }

    /// <summary>Number of employees left On Vacation.</summary>
    public int OnVacation { get; init; }

    /// <summary>Number of employees whose stored status actually changed this run.</summary>
    public int Changed { get; init; }

    /// <summary>A short, non-sensitive error description when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }
}
