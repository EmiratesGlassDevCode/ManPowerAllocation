using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Departments;

/// <summary>Use-case operations for maintaining departments and their requirements.</summary>
public interface IDepartmentService
{
    /// <summary>Returns all departments in a division, ordered by display sequence then name.</summary>
    /// <param name="division">The division to list.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<DepartmentDto>> GetByDivisionAsync(Division division, CancellationToken cancellationToken = default);

    /// <summary>Creates a new department, rejecting a duplicate (division, name) pair.</summary>
    /// <param name="request">The department to create.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a department's requirements, sequence and active flag (name and division are immutable).</summary>
    /// <param name="departmentId">The department to update.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<DepartmentDto> UpdateAsync(int departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Switches a department ON or OFF.</summary>
    /// <param name="departmentId">The department to toggle.</param>
    /// <param name="isActive">The new active state.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<DepartmentDto> SetActiveAsync(int departmentId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Deletes a department that has no employees allocated to it.</summary>
    /// <param name="departmentId">The department to delete.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task DeleteAsync(int departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every department across all divisions that has no employees allocated to it,
    /// ordered by division then name. Used by the cleanup screen to surface departments that
    /// are safe to remove (for example, ones created by a mistaken import).
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<DepartmentDto>> GetEmptyDepartmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the given departments, but only those that still have no employees allocated —
    /// any that have gained staff are skipped, never orphaned. Each deletion is audited. Returns
    /// how many were removed, skipped (had employees) or not found.
    /// </summary>
    /// <param name="departmentIds">The department ids selected for removal.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<DepartmentCleanupResult> DeleteEmptyDepartmentsAsync(IReadOnlyCollection<int> departmentIds, CancellationToken cancellationToken = default);
}
