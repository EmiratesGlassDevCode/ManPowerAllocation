using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Auditing;

/// <summary>A single audit trail entry as presented on the administration screen.</summary>
public sealed record AuditLogDto(
    long Id,
    string UserId,
    string? UserDisplayName,
    DateTime TimestampUtc,
    AuditAction Action,
    string EntityName,
    string? RecordId,
    string? OldValue,
    string? NewValue,
    bool IsBreakGlassSession);
