using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>One employee's home allocation as captured in a <see cref="MasterSnapshot"/>.</summary>
public sealed class MasterSnapshotEmployee
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key to the owning snapshot.</summary>
    public long SnapshotId { get; set; }

    /// <summary>Navigation to the owning snapshot.</summary>
    public MasterSnapshot? Snapshot { get; set; }

    /// <summary>The source employee id at capture time.</summary>
    public int EmployeeId { get; set; }

    /// <summary>Employee name at capture time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Badge / employee number at capture time, if any.</summary>
    public string? BadgeNumber { get; set; }

    /// <summary>Division of the home department at capture time.</summary>
    public Division Division { get; set; }

    /// <summary>Home department name at capture time.</summary>
    public string HomeDepartmentName { get; set; } = string.Empty;

    /// <summary>Assigned shift at capture time.</summary>
    public ShiftType Shift { get; set; }

    /// <summary>Whether the employee was an outsource / supply worker.</summary>
    public bool IsSupply { get; set; }
}
