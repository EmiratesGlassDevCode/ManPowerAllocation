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

    /// <summary>
    /// Grace, in minutes, applied on both sides of the shift-day boundary. Each operational day
    /// is treated as present from (day start − grace) until (next day start + grace), so the
    /// windows of consecutive days overlap by twice this value around the boundary. With the
    /// default 60 and a 07:00 day start, an early arrival from 06:00 is already counted and the
    /// outgoing shift keeps showing until 08:00, after which it drops on the clock — no dependence
    /// on punch-out times. Set to 0 for a hard cutover exactly at the shift start.
    /// </summary>
    public int BoundaryGraceMinutes { get; set; } = 60;

    /// <summary>The external attendance view's schema.</summary>
    public string ViewSchema { get; set; } = "dbo";

    /// <summary>The external attendance view's name.</summary>
    public string ViewName { get; set; } = "MPA";

    /// <summary>Column in the view holding the employee identifier (maps to <c>Employee.BadgeNumber</c>).</summary>
    public string EmployeeIdColumn { get; set; } = "EmpID";

    /// <summary>Column in the view holding the attendance date.</summary>
    public string DateColumn { get; set; } = "dt";

    /// <summary>Column in the view holding the check-in time.</summary>
    public string InTimeColumn { get; set; } = "InTime";

    /// <summary>Column in the view holding the check-out time.</summary>
    public string OutTimeColumn { get; set; } = "OutTime";
}
