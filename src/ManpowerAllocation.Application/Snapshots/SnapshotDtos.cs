using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Snapshots;

/// <summary>Header summary of a captured daily report, with its factory-wide rollup.</summary>
public sealed record SnapshotSummaryDto(
    long Id,
    DateTime OperationalDate,
    ShiftType Shift,
    DateTime CapturedAtUtc,
    string Source,
    int OnRoll,
    int Present,
    int Absent,
    int OnVacation,
    int SupplyPresent,
    int TotalPresent,
    int Required,
    int Variance,
    int ShortageDepartmentCount);

/// <summary>A department row within a captured snapshot.</summary>
public sealed record SnapshotDepartmentDto(
    int DepartmentId,
    Division Division,
    string DepartmentName,
    bool IsActive,
    int Required,
    int OnRoll,
    int Present,
    int Absent,
    int OnVacation,
    int SupplyPresent,
    int TotalPresent,
    int Variance,
    string Status);

/// <summary>An employee allocation line within a captured snapshot.</summary>
public sealed record SnapshotEmployeeDto(
    int EmployeeId,
    string Name,
    string? BadgeNumber,
    Division Division,
    string DepartmentName,
    ShiftType Shift,
    AttendanceStatus Status,
    bool IsSupply);

/// <summary>A captured snapshot with its full per-department and per-employee detail.</summary>
public sealed record SnapshotDetailDto(
    SnapshotSummaryDto Summary,
    IReadOnlyList<SnapshotDepartmentDto> Departments,
    IReadOnlyList<SnapshotEmployeeDto> Employees);
