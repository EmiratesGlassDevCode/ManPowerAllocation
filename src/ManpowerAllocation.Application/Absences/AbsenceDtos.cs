using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Absences;

// ── Reason categories (admin-managed) ───────────────────────────────────────────────────

/// <summary>An absence sub-category as shown on the admin screen and the reason drop-down.</summary>
public sealed record AbsenceCategoryDto(int Id, AbsenceKind Kind, string Name, int Sequence, bool IsActive);

/// <summary>Request to create a new absence sub-category under a fixed kind.</summary>
public sealed class CreateAbsenceCategoryRequest
{
    /// <summary>The fixed top-level reason the new category belongs to.</summary>
    public AbsenceKind Kind { get; set; }

    /// <summary>The category name (unique within its kind).</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>Request to rename / re-order / activate a category.</summary>
public sealed class UpdateAbsenceCategoryRequest
{
    /// <summary>The new category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The sort order within its kind.</summary>
    public int Sequence { get; set; }

    /// <summary>Whether the category is offered for new absences.</summary>
    public bool IsActive { get; set; } = true;
}

// ── Absentees list + reason recording ───────────────────────────────────────────────────

/// <summary>The active reason attached to an absent employee, if one has been recorded.</summary>
public sealed record AbsenceReasonView(
    int RecordId,
    int CategoryId,
    string CategoryName,
    AbsenceKind Kind,
    DateOnly FromDate,
    DateOnly? ToDate,
    string? Comment,
    string? SetByName);

/// <summary>One row on the Absentees page: an absent (or on-leave) employee and their reason.</summary>
public sealed record AbsenceListItemDto(
    int EmployeeId,
    string Name,
    string? BadgeNumber,
    int DepartmentId,
    string DepartmentName,
    Division Division,
    ShiftType Shift,
    AttendanceStatus Status,
    bool IsSupply,
    AbsenceReasonView? Reason);

/// <summary>Request to set (create or replace) the current absence reason for an employee.</summary>
public sealed class SetAbsenceReasonRequest
{
    /// <summary>The absent employee.</summary>
    public int EmployeeId { get; set; }

    /// <summary>The chosen (active) reason category.</summary>
    public int CategoryId { get; set; }

    /// <summary>Start of an Informed absence. Ignored / defaulted to today for Not-Informed.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>End of an Informed absence (required for Informed). Null for Not-Informed.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Free-text comment. The main field for a Not-Informed absence.</summary>
    public string? Comment { get; set; }
}
