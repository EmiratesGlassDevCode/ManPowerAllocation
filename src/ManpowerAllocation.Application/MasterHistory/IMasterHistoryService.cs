namespace ManpowerAllocation.Application.MasterHistory;

/// <summary>Read-only access to the captured master-data history. Admin only.</summary>
public interface IMasterHistoryService
{
    /// <summary>Lists captured master snapshots, newest first.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<MasterSnapshotSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
}
