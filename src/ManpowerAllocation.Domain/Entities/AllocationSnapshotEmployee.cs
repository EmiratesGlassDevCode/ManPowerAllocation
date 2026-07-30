using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// One employee's allocation line in a captured daily report: where the person was assigned and
/// their attendance state for the snapshot's shift. This per-employee detail is what lets later
/// analytics answer "who was allocated where" and "who was short", not just the department totals.
/// </summary>
public sealed class AllocationSnapshotEmployee
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key to the owning <see cref="AllocationSnapshot"/>.</summary>
    public long SnapshotId { get; set; }

    /// <summary>Navigation to the owning snapshot.</summary>
    public AllocationSnapshot? Snapshot { get; set; }

    /// <summary>The source employee id at capture time.</summary>
    public int EmployeeId { get; set; }

    /// <summary>The employee name at capture time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The badge / employee number at capture time, if any.</summary>
    public string? BadgeNumber { get; set; }

    /// <summary>The division the employee sat in.</summary>
    public Division Division { get; set; }

    /// <summary>The department name the employee was allocated to at capture time.</summary>
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>The employee's shift (matches the snapshot's shift).</summary>
    public ShiftType Shift { get; set; }

    /// <summary>The employee's attendance status at capture time.</summary>
    public AttendanceStatus Status { get; set; }

    /// <summary>Whether the employee was an outsource / supply worker.</summary>
    public bool IsSupply { get; set; }
}
