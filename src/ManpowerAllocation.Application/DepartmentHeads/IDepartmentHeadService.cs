namespace ManpowerAllocation.Application.DepartmentHeads;

/// <summary>
/// Manages department-head appointments (Admin only) and exposes the current user's own managed
/// department set for UI scoping. Every change is audited.
/// </summary>
public interface IDepartmentHeadService
{
    /// <summary>Lists all department heads with the departments they manage. Requires Admin.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<DepartmentHeadDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Appoints or re-scopes a department head: sets their role to DepartmentHead and replaces their
    /// managed departments with the supplied set. An empty set revokes the headship (role reset to
    /// Viewer). Requires Admin.
    /// </summary>
    /// <param name="request">The appointment details.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task UpsertAsync(UpsertDepartmentHeadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Revokes a headship entirely (removes all scope and resets the role to Viewer). Requires Admin.</summary>
    /// <param name="entraObjectId">The head's Entra object id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task RevokeAsync(string entraObjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the department ids the current user manages (empty unless they are a department head).
    /// Used by the UI to decide which departments to show edit controls for.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<int>> GetManagedDepartmentIdsAsync(CancellationToken cancellationToken = default);
}
