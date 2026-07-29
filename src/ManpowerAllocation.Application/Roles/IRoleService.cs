using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Roles;

/// <summary>
/// Administration of the Entra-object-id-to-role mapping. This is the only user data
/// the application maintains; no credential is ever stored or handled here.
/// </summary>
public interface IRoleService
{
    /// <summary>Returns all role assignments.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<RoleAssignmentDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves the role for an Entra object id, or <c>null</c> when the user has no assignment.</summary>
    /// <param name="entraObjectId">The Entra object id to look up.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<UserRole?> GetRoleAsync(string entraObjectId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a user's role assignment.</summary>
    /// <param name="request">The assignment to apply.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<RoleAssignmentDto> UpsertAsync(UpsertRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes a role assignment.</summary>
    /// <param name="assignmentId">The assignment to remove.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task DeleteAsync(int assignmentId, CancellationToken cancellationToken = default);
}
