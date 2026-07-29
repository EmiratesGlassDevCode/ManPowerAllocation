namespace ManpowerAllocation.Domain.Enums;

/// <summary>
/// The working shift an employee is assigned to. The prototype only ever stored
/// "DAY" or "NIGHT"; any other value was dropped on import.
/// </summary>
public enum ShiftType
{
    /// <summary>Day shift.</summary>
    Day = 1,

    /// <summary>Night shift.</summary>
    Night = 2
}
