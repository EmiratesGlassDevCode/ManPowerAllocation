using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Dashboard;

/// <summary>Read-only queries that compute the dashboard staffing views.</summary>
public interface IDashboardService
{
    /// <summary>
    /// Builds the dashboard for a single division under the given shift filter, including
    /// per-department statistics ordered by display sequence.
    /// </summary>
    /// <param name="division">The division to build.</param>
    /// <param name="shift">The active shift filter.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<DivisionDashboard> GetDivisionDashboardAsync(Division division, ShiftFilter shift, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the factory-wide summary (per-division totals plus the combined factory total)
    /// under the given shift filter.
    /// </summary>
    /// <param name="shift">The active shift filter.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<FactorySummary> GetFactorySummaryAsync(ShiftFilter shift, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the shift filter for the shift running now in factory-local time, using the
    /// configured day/night start times. Dashboards open on this so the default view reflects the
    /// shift actually on the floor — never a day+night pool that would count the off-shift as absent.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ShiftFilter> GetLiveShiftAsync(CancellationToken cancellationToken = default);
}
