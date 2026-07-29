namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>Configuration for the external attendance integration.</summary>
public sealed class AttendanceOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Attendance";

    /// <summary>Whether the scheduled synchronisation is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minutes between scheduled synchronisations.</summary>
    public int SyncIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Time zone used to determine "today" for check-ins. Accepts a Windows id (e.g.
    /// "Arabian Standard Time") or an IANA id (e.g. "Asia/Dubai"); .NET resolves either.
    /// </summary>
    public string TimeZoneId { get; set; } = "Arabian Standard Time";
}
