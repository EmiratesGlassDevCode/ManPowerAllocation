using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>One department's headcount targets as captured in a <see cref="MasterSnapshot"/>.</summary>
public sealed class MasterSnapshotDepartment
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key to the owning snapshot.</summary>
    public long SnapshotId { get; set; }

    /// <summary>Navigation to the owning snapshot.</summary>
    public MasterSnapshot? Snapshot { get; set; }

    /// <summary>Department name at capture time.</summary>
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>Division at capture time.</summary>
    public Division Division { get; set; }

    /// <summary>Required day-shift headcount at capture time.</summary>
    public int RequiredDay { get; set; }

    /// <summary>Required night-shift headcount at capture time.</summary>
    public int RequiredNight { get; set; }

    /// <summary>Whether the department was a shared pool at capture time.</summary>
    public bool IsPool { get; set; }
}
