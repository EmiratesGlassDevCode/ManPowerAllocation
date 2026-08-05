using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// A department within a division, carrying the day- and night-shift headcount
/// requirements against which attendance is measured. Uniqueness is (Division, Name);
/// the department name is the join key used throughout the domain and is always
/// stored trimmed and upper-cased.
/// </summary>
public sealed class Department
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>The division this department belongs to. A department name belongs to exactly one division.</summary>
    public Division Division { get; set; }

    /// <summary>Department name, always persisted trimmed and upper-cased (e.g. "CUTTING", "QUALITY CONTROL").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Required headcount for the day shift.</summary>
    public int RequiredDay { get; set; }

    /// <summary>Required headcount for the night shift.</summary>
    public int RequiredNight { get; set; }

    /// <summary>Display sort order within the division; lower values appear first.</summary>
    public decimal Sequence { get; set; }

    /// <summary>
    /// Whether the department is currently switched ON. A department that is toggled OFF
    /// contributes its present staff to the division's "available / excess" pool instead
    /// of counting towards a requirement.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The shift schedule this department follows (defines its day/night windows and grace). Defaults
    /// to schedule 1 for new and existing departments; an administrator reassigns it as needed.
    /// </summary>
    public int ShiftScheduleId { get; set; } = 1;

    /// <summary>Navigation to the owning shift schedule.</summary>
    public ShiftSchedule? ShiftSchedule { get; set; }

    /// <summary>Employees whose current department is this one.</summary>
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    /// <summary>
    /// Optimistic-concurrency token. Detects a lost update when two administrators change the
    /// same department at once; the second save then fails rather than silently overwriting.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
