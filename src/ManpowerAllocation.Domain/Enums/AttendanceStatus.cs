namespace ManpowerAllocation.Domain.Enums;

/// <summary>
/// An employee's attendance state for the day. Outsource / agency workers are
/// tracked by the <see cref="Entities.Employee.IsSupply"/> flag rather than a
/// separate status value, mirroring the source prototype.
/// </summary>
public enum AttendanceStatus
{
    /// <summary>Present ("YES" in the prototype).</summary>
    Present = 1,

    /// <summary>Absent ("NO" in the prototype).</summary>
    Absent = 2,

    /// <summary>On vacation / leave ("VAC" in the prototype).</summary>
    OnVacation = 3
}
