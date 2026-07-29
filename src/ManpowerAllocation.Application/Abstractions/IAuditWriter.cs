using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Writes entries to the audit trail. Every state-changing operation calls this so
/// that a create, update or delete is never persisted without a matching audit record.
/// The writer stamps the actor, timestamp and break-glass flag from <see cref="ICurrentUser"/>.
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Adds an audit entry describing a single change to the change tracker. The entry is
    /// committed together with the entity change in the same transaction / SaveChanges call,
    /// guaranteeing the audit row is written before the operation completes.
    /// </summary>
    /// <param name="action">Whether the change is a create, update or delete.</param>
    /// <param name="entityName">The logical entity type affected (e.g. "Department").</param>
    /// <param name="recordId">The primary key of the affected record, as text.</param>
    /// <param name="oldValue">The entity object before the change, serialised to JSON by the writer (null for creates).</param>
    /// <param name="newValue">The entity object after the change, serialised to JSON by the writer (null for deletes).</param>
    void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue);
}
