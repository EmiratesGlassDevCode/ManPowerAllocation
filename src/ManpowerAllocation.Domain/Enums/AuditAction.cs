namespace ManpowerAllocation.Domain.Enums;

/// <summary>
/// The category of state-changing operation recorded in the audit trail.
/// Every create, update and delete must be persisted with one of these values.
/// </summary>
public enum AuditAction
{
    /// <summary>A new record was created.</summary>
    Create = 1,

    /// <summary>An existing record was modified.</summary>
    Update = 2,

    /// <summary>A record was deleted.</summary>
    Delete = 3,

    /// <summary>A user signed in via Entra ID (or the emergency account).</summary>
    SignIn = 4,

    /// <summary>A user signed out.</summary>
    SignOut = 5
}
