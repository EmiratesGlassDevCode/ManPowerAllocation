namespace ManpowerAllocation.Application.MasterHistory;

/// <summary>A captured master-data version, for the history list.</summary>
public sealed record MasterSnapshotSummaryDto(
    long Id,
    DateTime CapturedAtUtc,
    string? CapturedByName,
    string Source,
    int EmployeeCount,
    int DepartmentCount);
