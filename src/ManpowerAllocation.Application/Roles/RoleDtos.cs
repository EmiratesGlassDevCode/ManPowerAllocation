using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Roles;

/// <summary>A role assignment as returned to administration screens.</summary>
public sealed record RoleAssignmentDto(
    int Id,
    string EntraObjectId,
    string? DisplayName,
    UserRole Role,
    DateTime CreatedAtUtc);

/// <summary>Request to create or update a user's role assignment.</summary>
public sealed record UpsertRoleRequest
{
    /// <summary>The Entra ID object identifier (GUID) of the user.</summary>
    public string EntraObjectId { get; init; } = string.Empty;

    /// <summary>An optional display name cached for the administration screen.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The role to grant.</summary>
    public UserRole Role { get; init; }
}
