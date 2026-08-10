using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.DepartmentHeads;

/// <summary>A department head and the departments they manage.</summary>
public sealed record DepartmentHeadDto(
    string EntraObjectId,
    string? DisplayName,
    IReadOnlyList<DepartmentHeadScopeDto> Departments);

/// <summary>One department within a head's scope.</summary>
public sealed record DepartmentHeadScopeDto(int DepartmentId, string DepartmentName, Division Division);

/// <summary>
/// Request to appoint (or re-scope) a department head. Sets the user's role to
/// <see cref="UserRole.DepartmentHead"/> and replaces their managed-department set with the supplied
/// list. An empty list revokes the headship.
/// </summary>
public sealed record UpsertDepartmentHeadRequest
{
    /// <summary>The head's Entra ID object identifier.</summary>
    public string EntraObjectId { get; init; } = string.Empty;

    /// <summary>The head's display name (stored on the role assignment).</summary>
    public string? DisplayName { get; init; }

    /// <summary>The departments this person will manage.</summary>
    public IReadOnlyList<int> DepartmentIds { get; init; } = Array.Empty<int>();
}
