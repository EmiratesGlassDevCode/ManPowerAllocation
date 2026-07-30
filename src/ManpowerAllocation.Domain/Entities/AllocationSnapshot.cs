using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// A frozen copy of the daily staffing report for one operational date and shift, captured at the
/// shift cut-off (10:00 for the day shift, 22:00 for the night shift). Each snapshot carries the
/// factory-wide rollup for quick history listing, plus the full per-department and per-employee
/// detail so future analytics can drill in. Snapshots are immutable once written; re-capturing the
/// same (date, shift) replaces the previous one.
/// </summary>
public sealed class AllocationSnapshot
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The operational (calendar) date the snapshot belongs to, in the factory time zone (date component only).</summary>
    public DateTime OperationalDate { get; set; }

    /// <summary>The shift this snapshot captures.</summary>
    public ShiftType Shift { get; set; }

    /// <summary>When the snapshot was captured (UTC).</summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>How the snapshot was triggered (e.g. "auto-10:00", "auto-22:00", "manual").</summary>
    public string Source { get; set; } = string.Empty;

    // Factory-wide rollup for this shift (redundant with the department rows, but kept so the
    // history list renders without a join).

    /// <summary>Total own-headcount on roll for the shift.</summary>
    public int OnRoll { get; set; }

    /// <summary>Own staff present.</summary>
    public int Present { get; set; }

    /// <summary>Own staff absent.</summary>
    public int Absent { get; set; }

    /// <summary>Own staff on vacation.</summary>
    public int OnVacation { get; set; }

    /// <summary>Outsource / supply staff present.</summary>
    public int SupplyPresent { get; set; }

    /// <summary>Total present including supply.</summary>
    public int TotalPresent { get; set; }

    /// <summary>Total required headcount for the shift.</summary>
    public int Required { get; set; }

    /// <summary>Total present minus required (negative = short).</summary>
    public int Variance { get; set; }

    /// <summary>Number of departments running short of requirement.</summary>
    public int ShortageDepartmentCount { get; set; }

    /// <summary>The per-department report rows.</summary>
    public List<AllocationSnapshotDepartment> Departments { get; set; } = new();

    /// <summary>The per-employee allocation lines.</summary>
    public List<AllocationSnapshotEmployee> Employees { get; set; } = new();
}
