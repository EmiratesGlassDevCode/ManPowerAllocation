using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// A worker allocated to a department. The prototype had no stable identity for a
/// person (badge numbers repeat and outsource rows reuse small integers), so this
/// entity carries a surrogate <see cref="Id"/> and keeps <see cref="BadgeNumber"/>
/// as a non-unique reference field.
/// </summary>
public sealed class Employee
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Employee display name. Never used as an identity because it is not unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The badge / employee number from the source data. Not unique and may be blank for outsource rows.</summary>
    public string? BadgeNumber { get; set; }

    /// <summary>The division this employee currently sits in.</summary>
    public Division Division { get; set; }

    /// <summary>
    /// Foreign key to the employee's <em>current</em> department — where they are working right now.
    /// A shift loan changes this temporarily; the shift-reset returns it to <see cref="HomeDepartmentId"/>.
    /// </summary>
    public int DepartmentId { get; set; }

    /// <summary>Navigation to the current department.</summary>
    public Department? Department { get; set; }

    /// <summary>
    /// The employee's permanent "home" department, set only by the master-data import. Every in-app
    /// movement is a temporary loan; the shift-reset returns <see cref="DepartmentId"/> to this value.
    /// Null only until the first backfill/import runs (then it mirrors the current department).
    /// </summary>
    public int? HomeDepartmentId { get; set; }

    /// <summary>Assigned working shift.</summary>
    public ShiftType Shift { get; set; }

    /// <summary>Current attendance state for the day.</summary>
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    /// <summary>
    /// When a user manually marks the employee Present to correct a missed punch, this holds the
    /// factory-local instant until which that manual status is protected from the attendance sync.
    /// While it is in the future the sync will not revert the employee to Absent. Null when there is
    /// no active override; cleared automatically when it expires or when a real punch is seen.
    /// </summary>
    public DateTime? PresenceOverrideUntil { get; set; }

    /// <summary>The reason recorded for a manual presence override (defaults to "Missed punch").</summary>
    public string? PresenceOverrideReason { get; set; }

    /// <summary>
    /// True when this is an outsourced / agency worker. Supply workers do not count
    /// towards own headcount but a present supply worker does fill a required slot.
    /// </summary>
    public bool IsSupply { get; set; }

    /// <summary>Free-text notes such as sub-role or machine assignment (e.g. "PACKING", "DRIVER").</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. Detects a lost update when two people (or a person and the
    /// background sync) change the same employee at once; the second save then fails rather than
    /// silently overwriting the first.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
