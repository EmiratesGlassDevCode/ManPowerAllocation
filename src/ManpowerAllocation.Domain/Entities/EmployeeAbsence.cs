using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// A recorded reason for why an employee is absent. Created and edited from the Absentees page
/// by a department head (for their own departments) or any User/Admin.
/// <para>
/// An <see cref="AbsenceKind.Informed"/> record carries a <see cref="FromDate"/>/<see cref="ToDate"/>
/// window and is "active" only while today falls within it — once <see cref="ToDate"/> passes the
/// record stops applying and the employee drops off the current absentees list (the row is retained
/// for history/audit, never silently deleted). A <see cref="AbsenceKind.NotInformed"/> record has no
/// end date (<see cref="ToDate"/> is null) and carries only a <see cref="Comment"/>; it applies from
/// <see cref="FromDate"/> until the employee returns or the reason is cleared.
/// </para>
/// </summary>
public sealed class EmployeeAbsence
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to the absent <see cref="Employee"/>.</summary>
    public int EmployeeId { get; set; }

    /// <summary>Navigation to the absent employee.</summary>
    public Employee? Employee { get; set; }

    /// <summary>Foreign key to the chosen <see cref="AbsenceReasonCategory"/>.</summary>
    public int CategoryId { get; set; }

    /// <summary>Navigation to the chosen reason category.</summary>
    public AbsenceReasonCategory? Category { get; set; }

    /// <summary>The top-level reason, denormalised from the category for straightforward filtering.</summary>
    public AbsenceKind Kind { get; set; }

    /// <summary>The first day the absence applies (defaults to today for a Not-Informed record).</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>The last day an Informed absence applies. Null for a Not-Informed (open-ended) record.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Free-text note. The primary field for a Not-Informed absence; optional for Informed.</summary>
    public string? Comment { get; set; }

    /// <summary>Entra object id (or break-glass id) of whoever recorded the reason.</summary>
    public string CreatedByObjectId { get; set; } = string.Empty;

    /// <summary>Display name of whoever recorded the reason, captured at the time.</summary>
    public string? CreatedByName { get; set; }

    /// <summary>UTC timestamp the reason was recorded or last updated.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Optimistic-concurrency token.</summary>
    public byte[]? RowVersion { get; set; }
}
