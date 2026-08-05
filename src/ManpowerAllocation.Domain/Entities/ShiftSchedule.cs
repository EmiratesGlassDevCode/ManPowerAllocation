namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// A named shift schedule that a department follows. Each schedule is a 12-hour day shift starting
/// at <see cref="DayStart"/> and a 12-hour night shift starting twelve hours later, with a
/// symmetric <see cref="GraceMinutes"/> tolerance applied before the start and after the end of each
/// half (so an early check-in and a late check-out still count). Departments on different schedules
/// (for example 06:00–18:00 vs 07:00–19:00) are evaluated against their own windows.
/// </summary>
public sealed class ShiftSchedule
{
    /// <summary>Surrogate primary key. Schedule 1 is the default assigned to new departments.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable name shown in the admin UI (e.g. "07:00 – 19:00").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Local (factory) start time of the day shift; the night shift starts twelve hours later.</summary>
    public TimeSpan DayStart { get; set; }

    /// <summary>Grace tolerance in minutes applied before the start and after the end of each shift half.</summary>
    public int GraceMinutes { get; set; }
}
