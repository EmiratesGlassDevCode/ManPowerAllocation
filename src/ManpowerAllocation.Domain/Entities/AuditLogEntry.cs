using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// An immutable record of a single state-changing operation. Every create, update
/// and delete must write one of these before the operation is allowed to complete.
/// Old and new values are stored as JSON snapshots so the change is fully reconstructable.
/// </summary>
public sealed class AuditLogEntry
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Identity of the actor. For normal sessions this is the Entra object id; for a
    /// break-glass session it is the fixed break-glass account identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Cached display name of the actor at the time of the action.</summary>
    public string? UserDisplayName { get; set; }

    /// <summary>UTC timestamp the action occurred.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>The kind of change (create / update / delete).</summary>
    public AuditAction Action { get; set; }

    /// <summary>The logical table or entity type affected (e.g. "Department", "Employee").</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>The primary key of the affected record, rendered as text.</summary>
    public string? RecordId { get; set; }

    /// <summary>JSON snapshot of the record before the change (null for creates).</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON snapshot of the record after the change (null for deletes).</summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// True when the action was performed inside an emergency break-glass session
    /// (i.e. outside normal Entra ID authentication). This makes such actions
    /// unmistakable when reviewing the trail afterwards.
    /// </summary>
    public bool IsBreakGlassSession { get; set; }
}
