namespace ManpowerAllocation.Application.Allocation;

/// <summary>
/// Drives the shift-loan reset: returning every loaned employee to their home department. The reset
/// runs automatically at each shift changeover when enabled, and can be triggered manually.
/// </summary>
public interface IAllocationResetService
{
    /// <summary>Returns the current reset status (enabled, gate, counts) for the admin screen. Admin only.</summary>
    Task<AllocationResetStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Enables or disables the automatic shift-reset. Admin only; enabling requires the gate to be met.</summary>
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Immediately returns all loaned employees to their home department. Requires User/Admin. Returns the count.</summary>
    Task<int> ResetNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called by the background worker: if the reset is enabled and a new shift boundary has been
    /// crossed since the last run, returns loaned employees home. No-op otherwise. Not role-checked.
    /// </summary>
    Task ProcessBoundaryAsync(CancellationToken cancellationToken = default);
}
