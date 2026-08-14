using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Analytics;

/// <summary>
/// Read-only analytics over the captured daily-report history and recorded absences. Every query is
/// bounded by an inclusive From/To operational-date range. Requires the Viewer role (enforced by the
/// page and endpoint policies).
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Company-wide staffing trend: one point per captured date + shift.</summary>
    Task<IReadOnlyList<OverallTrendPoint>> GetOverallTrendAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Per-department staffing performance aggregated across the range.</summary>
    Task<IReadOnlyList<DepartmentTrendRow>> GetDepartmentTrendAsync(DateTime from, DateTime to, Division? division, CancellationToken cancellationToken = default);

    /// <summary>Absence breakdowns (by reason and by department) over the range.</summary>
    Task<AbsenteeAnalyticsDto> GetAbsenteeAnalyticsAsync(DateTime from, DateTime to, Division? division, CancellationToken cancellationToken = default);

    /// <summary>One employee's captured attendance history and recorded absence reasons across the range.</summary>
    Task<EmployeeHistoryDto?> GetEmployeeHistoryAsync(int employeeId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>The employees available to pick for the employee-history report.</summary>
    Task<IReadOnlyList<EmployeeOption>> GetEmployeeOptionsAsync(CancellationToken cancellationToken = default);
}
