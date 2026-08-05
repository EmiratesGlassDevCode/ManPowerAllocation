using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Shifts;

/// <summary>A shift schedule as shown to administrators.</summary>
/// <param name="Id">Schedule id.</param>
/// <param name="Name">Display name.</param>
/// <param name="DayStart">Day-shift start time.</param>
/// <param name="NightStart">Night-shift start time (day start + 12h).</param>
/// <param name="GraceMinutes">Grace tolerance applied before start and after end of each half.</param>
public sealed record ShiftScheduleDto(int Id, string Name, TimeSpan DayStart, TimeSpan NightStart, int GraceMinutes);

/// <summary>A department and the schedule it is assigned to.</summary>
public sealed record DepartmentScheduleDto(int DepartmentId, string DepartmentName, Division Division, int ShiftScheduleId, string ScheduleName);

/// <summary>Request to assign a department to a shift schedule.</summary>
public sealed record AssignScheduleRequest(int ShiftScheduleId);

/// <summary>Request to create a shift schedule.</summary>
public sealed record CreateShiftScheduleRequest
{
    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Day-shift start time; night starts twelve hours later.</summary>
    public TimeSpan DayStart { get; init; }

    /// <summary>Grace tolerance in minutes (before start / after end).</summary>
    public int GraceMinutes { get; init; }
}

/// <summary>Request to update a shift schedule.</summary>
public sealed record UpdateShiftScheduleRequest
{
    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Day-shift start time; night starts twelve hours later.</summary>
    public TimeSpan DayStart { get; init; }

    /// <summary>Grace tolerance in minutes (before start / after end).</summary>
    public int GraceMinutes { get; init; }
}
