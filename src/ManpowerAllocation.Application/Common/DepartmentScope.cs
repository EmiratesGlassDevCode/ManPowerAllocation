using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Common;

/// <summary>
/// Helpers for department-scoped authorization. A <see cref="UserRole.DepartmentHead"/> may act only
/// on the departments assigned to it (via <c>DepartmentManagers</c>); everyone else is governed by
/// the global role ladder. Enforcement lives in the services (server-side), never in the UI.
/// </summary>
internal static class DepartmentScope
{
    /// <summary>Returns true when the current user is a department head assigned to the given department.</summary>
    /// <param name="dbContext">The persistence context.</param>
    /// <param name="currentUser">The current principal.</param>
    /// <param name="departmentId">The department being acted on.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public static async Task<bool> IsHeadOfAsync(
        IApplicationDbContext dbContext, ICurrentUser currentUser, int departmentId, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.DepartmentHead || string.IsNullOrEmpty(currentUser.UserId))
        {
            return false;
        }

        return await dbContext.DepartmentManagers
            .AsNoTracking()
            .AnyAsync(m => m.EntraObjectId == currentUser.UserId && m.DepartmentId == departmentId, cancellationToken);
    }
}
