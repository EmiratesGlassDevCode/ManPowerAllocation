namespace ManpowerAllocation.Application.Attendance;

/// <summary>
/// Tuning for how the attendance sync scopes presence to a shift. The grace window captures the
/// early comer and the late leaver: a shift is treated as "live" from <see cref="GraceMinutes"/>
/// before its official start, and an employee is only auto-marked present/absent while their own
/// shift is live — so a night worker is never shown absent during the morning, and vice versa.
/// </summary>
public sealed class ShiftWindowOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Attendance";

    /// <summary>Minutes of tolerance before a shift's official start (and after its end). Default 60.</summary>
    public int GraceMinutes { get; set; } = 60;
}
