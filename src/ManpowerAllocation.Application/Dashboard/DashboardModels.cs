using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Dashboard;

/// <summary>Shift selector used by the dashboards. "All" combines day and night.</summary>
public enum ShiftFilter
{
    /// <summary>Both day and night shifts combined.</summary>
    All = 0,

    /// <summary>Day shift only.</summary>
    Day = 1,

    /// <summary>Night shift only.</summary>
    Night = 2
}

/// <summary>The staffing state of a department relative to its requirement.</summary>
public enum DepartmentStaffingStatus
{
    /// <summary>Department is switched OFF; its present staff are available elsewhere.</summary>
    Off = 0,

    /// <summary>Staffing deficit exceeds ten percent of the requirement.</summary>
    Short = 1,

    /// <summary>Staffing meets the requirement within tolerance.</summary>
    Optimal = 2,

    /// <summary>Present staff exceed the requirement.</summary>
    Excess = 3
}

/// <summary>Computed staffing statistics for a single department under a shift filter.</summary>
public sealed record DepartmentStats(
    int DepartmentId,
    string Name,
    Division Division,
    bool IsActive,
    int Required,
    int OnRoll,
    int Present,
    int Absent,
    int OnVacation,
    int SupplyPresent,
    int TotalPresent,
    int Variance,
    DepartmentStaffingStatus Status);

/// <summary>Rolled-up staffing statistics for a whole division under a shift filter.</summary>
public sealed record DivisionTotals(
    Division Division,
    int OnRoll,
    int Present,
    int Absent,
    int OnVacation,
    int SupplyPresent,
    int TotalPresent,
    int Required,
    int Variance,
    int ShortageDepartmentCount);

/// <summary>The full dashboard payload for one division under a shift filter.</summary>
public sealed record DivisionDashboard(
    DivisionTotals Totals,
    IReadOnlyList<DepartmentStats> Departments);

/// <summary>
/// Factory-wide summary: the totals for each division plus the combined factory totals,
/// under a single shift filter. Used by the top-level Summary screen.
/// </summary>
public sealed record FactorySummary(
    IReadOnlyList<DivisionTotals> Divisions,
    DivisionTotals FactoryTotal);
