using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Analytics;

// ── Overall staffing trend ──────────────────────────────────────────────────────────────

/// <summary>One captured point (a date + shift) on the company-wide staffing trend.</summary>
public sealed record OverallTrendPoint(
    DateTime Date,
    string Shift,
    int Required,
    int TotalPresent,
    int Absent,
    int OnVacation,
    int Supply,
    int Variance,
    int FillPct,
    int ShortageDepartments);

// ── Department trend ────────────────────────────────────────────────────────────────────

/// <summary>Aggregated staffing performance for one department across the selected range.</summary>
public sealed record DepartmentTrendRow(
    int DepartmentId,
    string Department,
    string Division,
    int Captures,
    double AvgRequired,
    double AvgPresent,
    int AvgFillPct,
    int DaysShort,
    int DaysExcess,
    int WorstVariance);

// ── Absentee analytics ──────────────────────────────────────────────────────────────────

/// <summary>Absence totals for one reason (kind + sub-category) over the range.</summary>
public sealed record AbsenceByCategoryRow(string Kind, string Category, int Records, int Employees);

/// <summary>Absence totals for one department over the range.</summary>
public sealed record AbsenceByDepartmentRow(string Division, string Department, int Records, int Employees);

/// <summary>One day on the absence trend: absent and on-vacation head counts (summed across shifts).</summary>
public sealed record AbsenceTrendPoint(DateTime Date, int Absent, int OnVacation);

/// <summary>Absenteeism rate for one department across the range.</summary>
public sealed record DepartmentAbsenceRateRow(string Division, string Department, double AvgAbsent, double AvgOnRoll, int AbsenceRatePct);

/// <summary>Informed-vs-not-informed compliance for one department across the range.</summary>
public sealed record DepartmentComplianceRow(string Division, string Department, int Informed, int NotInformed, int InformedPct);

/// <summary>An employee with a high count of absent days across the range.</summary>
public sealed record TopAbsenteeRow(int EmployeeId, string Name, string? Badge, string Department, int AbsentDays);

/// <summary>The absentee analytics result: reason and department breakdowns, trend, rate, compliance and top absentees.</summary>
public sealed record AbsenteeAnalyticsDto(
    IReadOnlyList<AbsenceByCategoryRow> ByCategory,
    IReadOnlyList<AbsenceByDepartmentRow> ByDepartment,
    int TotalRecords,
    int TotalEmployees,
    int InformedRecords,
    int NotInformedRecords,
    IReadOnlyList<AbsenceTrendPoint> Trend,
    IReadOnlyList<DepartmentAbsenceRateRow> ByDepartmentRate,
    IReadOnlyList<DepartmentComplianceRow> Compliance,
    IReadOnlyList<TopAbsenteeRow> TopAbsentees);

// ── Employee history ────────────────────────────────────────────────────────────────────

/// <summary>One captured day+shift attendance line for a single employee.</summary>
public sealed record EmployeeDayStatus(DateTime Date, string Shift, string Department, string Status);

/// <summary>One recorded absence reason for the employee intersecting the range.</summary>
public sealed record EmployeeAbsenceRow(string Kind, string Category, DateOnly FromDate, DateOnly? ToDate, string? Comment, string? SetBy);

/// <summary>The full employee history: captured attendance lines and recorded absence reasons.</summary>
public sealed record EmployeeHistoryDto(
    int EmployeeId,
    string Name,
    string? Badge,
    int PresentDays,
    int AbsentDays,
    int VacationDays,
    IReadOnlyList<EmployeeDayStatus> Days,
    IReadOnlyList<EmployeeAbsenceRow> Absences);

/// <summary>A selectable employee for the employee-history picker.</summary>
public sealed record EmployeeOption(int Id, string Name, string? Badge, string Department, Division Division);
