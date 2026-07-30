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
    /// Time zone recorded for reference/diagnostics. The view now owns the shift-boundary logic,
    /// so this no longer drives presence. Accepts a Windows id (e.g. "Arabian Standard Time") or
    /// an IANA id (e.g. "Asia/Dubai").
    /// </summary>
    public string TimeZoneId { get; set; } = "Arabian Standard Time";

    /// <summary>The external attendance view's schema.</summary>
    public string ViewSchema { get; set; } = "dbo";

    /// <summary>The external attendance view's name.</summary>
    public string ViewName { get; set; } = "MPA";

    /// <summary>Column in the view holding the employee identifier (maps to <c>Employee.BadgeNumber</c>).</summary>
    public string EmployeeIdColumn { get; set; } = "EmpID";

    /// <summary>Column in the view holding the check-in time.</summary>
    public string InTimeColumn { get; set; } = "InTime";

    /// <summary>Column in the view holding the check-out time.</summary>
    public string OutTimeColumn { get; set; } = "OutTime";

    /// <summary>Column in the view holding the shift label the row belongs to.</summary>
    public string ShiftLabelColumn { get; set; } = "ShiftLabel";

    /// <summary>
    /// The <see cref="ShiftLabelColumn"/> value that marks a row as belonging to the live shift.
    /// Rows with this label and a non-null check-in time are counted as present. Matched
    /// case-insensitively by SQL Server's default collation.
    /// </summary>
    public string CurrentShiftValue { get; set; } = "Current Shift";
}
