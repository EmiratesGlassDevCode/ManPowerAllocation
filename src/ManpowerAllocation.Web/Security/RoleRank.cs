using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Maps an application role to its position on the global Viewer → User → Admin ladder used by the
/// "at least" authorization checks. <see cref="UserRole.DepartmentHead"/> is a scoped role: globally
/// it ranks only as <see cref="UserRole.Viewer"/> (read-only everywhere), and its extra abilities are
/// granted per department by the services, never by this ladder.
/// </summary>
internal static class RoleRank
{
    /// <summary>Returns the ladder rank of a role (DepartmentHead counts as Viewer).</summary>
    /// <param name="role">The role to rank.</param>
    public static int RankOf(UserRole role) =>
        role == UserRole.DepartmentHead ? (int)UserRole.Viewer : (int)role;
}
