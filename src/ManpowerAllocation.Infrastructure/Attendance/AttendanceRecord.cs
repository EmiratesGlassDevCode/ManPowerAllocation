namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// A read-only projection of one row of the external attendance view (schema/name configured
/// via <see cref="AttendanceOptions"/>, currently <c>[dbo].[MPA]</c>). Keyless: the application
/// never writes to it.
/// </summary>
public sealed class AttendanceRecord
{
    /// <summary>The employee identifier from the attendance system (maps to <c>Employee.BadgeNumber</c>).</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>The attendance date.</summary>
    public DateTime Dt { get; set; }

    /// <summary>The check-in time, if the employee has checked in.</summary>
    public DateTime? InTime { get; set; }

    /// <summary>The check-out time, if recorded.</summary>
    public DateTime? OutTime { get; set; }
}
