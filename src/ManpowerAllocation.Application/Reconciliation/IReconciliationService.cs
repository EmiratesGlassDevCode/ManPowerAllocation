namespace ManpowerAllocation.Application.Reconciliation;

/// <summary>
/// Compares the biometric attendance source against the governed roster to verify the sync pulled
/// real data whose counts reconcile, and to surface people who cannot be matched (punches with no
/// employee, and employees with no badge).
/// </summary>
public interface IReconciliationService
{
    /// <summary>Builds the current reconciliation and verification report.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ReconciliationReport> BuildAsync(CancellationToken cancellationToken = default);
}
