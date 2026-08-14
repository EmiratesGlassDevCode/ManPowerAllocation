namespace ManpowerAllocation.Domain.Enums;

/// <summary>
/// Application roles used for role-based access control. Roles are assigned to an
/// Entra ID object identifier through the <see cref="Entities.RoleAssignment"/> table;
/// the application database never stores a credential of any kind.
/// </summary>
public enum UserRole
{
    /// <summary>Read-only access to dashboards and reports.</summary>
    Viewer = 1,

    /// <summary>May record day-to-day attendance changes (status, shift, department moves).</summary>
    User = 2,

    /// <summary>Full control including master-data import, department maintenance and role administration.</summary>
    Admin = 3,

    /// <summary>
    /// Manages one or more specific departments (edit requirements, add/remove staff and roster
    /// actions within those departments) while seeing the rest of the factory read-only. This is a
    /// scoped role: globally it ranks only as <see cref="Viewer"/>, and its extra abilities apply
    /// only to the departments assigned to it (see <see cref="Entities.DepartmentManager"/>).
    /// </summary>
    DepartmentHead = 4
}
