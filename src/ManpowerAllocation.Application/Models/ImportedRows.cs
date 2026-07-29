using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Models;

/// <summary>
/// A single employee row parsed from an uploaded attendance workbook, already
/// normalised to domain enums by the parser. Rows the parser could not interpret
/// are omitted rather than represented here.
/// </summary>
public sealed record ImportedEmployeeRow
{
    /// <summary>Division the sheet belonged to.</summary>
    public Division Division { get; init; }

    /// <summary>Employee name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Badge / employee number, if present.</summary>
    public string? BadgeNumber { get; init; }

    /// <summary>Canonical (upper-cased) department name.</summary>
    public string DepartmentName { get; init; } = string.Empty;

    /// <summary>Assigned shift.</summary>
    public ShiftType Shift { get; init; }

    /// <summary>Attendance status.</summary>
    public AttendanceStatus Status { get; init; }

    /// <summary>Whether this is an outsource / supply worker.</summary>
    public bool IsSupply { get; init; }

    /// <summary>Free-text notes / designation.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// A single department requirement row parsed from an uploaded requirements workbook.
/// </summary>
public sealed record ImportedRequirementRow
{
    /// <summary>Division the requirement belongs to.</summary>
    public Division Division { get; init; }

    /// <summary>Canonical (upper-cased) department name.</summary>
    public string DepartmentName { get; init; } = string.Empty;

    /// <summary>Required day-shift headcount.</summary>
    public int RequiredDay { get; init; }

    /// <summary>Required night-shift headcount.</summary>
    public int RequiredNight { get; init; }
}
