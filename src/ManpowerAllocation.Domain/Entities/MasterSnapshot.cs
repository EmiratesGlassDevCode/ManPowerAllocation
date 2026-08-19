namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// A captured copy of the master data at the moment a master sheet was uploaded — each employee's
/// home department and shift, plus the department headcount targets. Retained as an immutable history
/// so any month's master can be reviewed or exported (XLS/PDF) later.
/// </summary>
public sealed class MasterSnapshot
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>UTC time the master was uploaded / captured.</summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>Entra object id of whoever uploaded the master.</summary>
    public string? CapturedByObjectId { get; set; }

    /// <summary>Display name of whoever uploaded the master.</summary>
    public string? CapturedByName { get; set; }

    /// <summary>How the master was applied (e.g. "Roster import", "Apply edits").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Number of employees captured.</summary>
    public int EmployeeCount { get; set; }

    /// <summary>Number of departments captured.</summary>
    public int DepartmentCount { get; set; }

    /// <summary>Per-employee home-allocation lines.</summary>
    public List<MasterSnapshotEmployee> Employees { get; set; } = new();

    /// <summary>Per-department requirement lines.</summary>
    public List<MasterSnapshotDepartment> Departments { get; set; } = new();
}
