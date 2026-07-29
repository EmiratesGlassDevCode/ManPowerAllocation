namespace ManpowerAllocation.Application.Auditing;

/// <summary>Read-only access to the audit trail for administrators.</summary>
public interface IAuditReadService
{
    /// <summary>
    /// Returns the most recent audit entries, newest first. Optionally filtered to only those
    /// recorded during an emergency break-glass session.
    /// </summary>
    /// <param name="take">The maximum number of entries to return (capped internally).</param>
    /// <param name="breakGlassOnly">When true, returns only break-glass-flagged entries.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take, bool breakGlassOnly, CancellationToken cancellationToken = default);
}
