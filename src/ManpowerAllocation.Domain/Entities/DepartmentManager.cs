namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// Assigns a department head (an Entra user) to a department they manage. A head may have several
/// rows (they manage several departments) and a department may have several heads. This table holds
/// only the scope; the head's application role lives in <see cref="RoleAssignment"/>. No credential
/// is ever stored.
/// </summary>
public sealed class DepartmentManager
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>The head's Entra ID object identifier.</summary>
    public string EntraObjectId { get; set; } = string.Empty;

    /// <summary>The department this head manages.</summary>
    public int DepartmentId { get; set; }

    /// <summary>Navigation to the managed department.</summary>
    public Department? Department { get; set; }

    /// <summary>When the assignment was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Entra object id of the administrator who created the assignment.</summary>
    public string? CreatedByObjectId { get; set; }
}
