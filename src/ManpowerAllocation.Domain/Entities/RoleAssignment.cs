using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// Maps an Entra ID object identifier to an application <see cref="UserRole"/>.
/// This is the ONLY user-related data the application stores: it never holds a
/// password, secret or any other credential. Authentication is performed entirely
/// by Entra ID; this table only authorises an already-authenticated principal.
/// </summary>
public sealed class RoleAssignment
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The Entra ID object identifier (the "oid" claim) of the user. This is a stable,
    /// tenant-scoped GUID and is the only user identifier stored by the application.
    /// </summary>
    public string EntraObjectId { get; set; } = string.Empty;

    /// <summary>A human-readable display name, cached for administration screens only.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The role granted to this user.</summary>
    public UserRole Role { get; set; }

    /// <summary>UTC timestamp the assignment was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The Entra object id of the administrator who created the assignment.</summary>
    public string? CreatedByObjectId { get; set; }
}
