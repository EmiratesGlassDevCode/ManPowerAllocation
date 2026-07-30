namespace ManpowerAllocation.Application.Snapshots;

/// <summary>Read-only queries over the captured daily-report history.</summary>
public interface IAllocationHistoryService
{
    /// <summary>
    /// Lists the snapshots whose operational date falls within the inclusive range, newest first.
    /// </summary>
    /// <param name="fromDate">Inclusive start date (date component used).</param>
    /// <param name="toDate">Inclusive end date (date component used).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<SnapshotSummaryDto>> ListAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>Returns one snapshot with its department and employee detail, or null if not found.</summary>
    /// <param name="id">The snapshot id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<SnapshotDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);
}
