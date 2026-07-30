using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// One department's line in a captured daily report: its requirement and present/absent/vacation
/// breakdown for the snapshot's shift. Mirrors the live staffing calculation so history and the
/// current view are computed the same way.
/// </summary>
public sealed class AllocationSnapshotDepartment
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key to the owning <see cref="AllocationSnapshot"/>.</summary>
    public long SnapshotId { get; set; }

    /// <summary>Navigation to the owning snapshot.</summary>
    public AllocationSnapshot? Snapshot { get; set; }

    /// <summary>The source department id at capture time (the department row may change later).</summary>
    public int DepartmentId { get; set; }

    /// <summary>The department's division.</summary>
    public Division Division { get; set; }

    /// <summary>The department name at capture time.</summary>
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>Whether the department was switched ON at capture time.</summary>
    public bool IsActive { get; set; }

    /// <summary>Required headcount for the shift.</summary>
    public int Required { get; set; }

    /// <summary>Own-headcount on roll.</summary>
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

    /// <summary>Total present minus required (negative = short).</summary>
    public int Variance { get; set; }

    /// <summary>The staffing status label at capture time (Off / Short / Optimal / Excess).</summary>
    public string Status { get; set; } = string.Empty;
}
