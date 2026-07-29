using System.Text.Json;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Infrastructure.Auditing;

/// <summary>
/// Default <see cref="IAuditWriter"/>. Adds an <see cref="AuditLogEntry"/> to the change
/// tracker (it is not saved here); the calling service commits it in the same
/// <c>SaveChanges</c> as the entity change, so a change and its audit row are always
/// written together. The actor and break-glass flag are taken from the current principal.
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // Compact snapshots; enums are written as their string names for readability.
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    /// <summary>Initialises the writer.</summary>
    /// <param name="dbContext">The context whose change tracker the entry is added to.</param>
    /// <param name="currentUser">The current principal, stamped onto every entry.</param>
    /// <param name="clock">Clock used for the audit timestamp.</param>
    public AuditWriter(IApplicationDbContext dbContext, ICurrentUser currentUser, IClock clock)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <inheritdoc />
    public void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue)
    {
        var entry = new AuditLogEntry
        {
            UserId = string.IsNullOrEmpty(_currentUser.UserId) ? "system" : _currentUser.UserId,
            UserDisplayName = _currentUser.DisplayName,
            TimestampUtc = _clock.UtcNow,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = Serialize(oldValue),
            NewValue = Serialize(newValue),
            // Any action performed during an emergency session is flagged so it is
            // unmistakable afterwards which changes happened outside Entra ID authentication.
            IsBreakGlassSession = _currentUser.IsBreakGlassSession
        };

        _dbContext.AuditLogEntries.Add(entry);
    }

    /// <summary>Serialises a snapshot to JSON, returning null for a null snapshot.</summary>
    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, SerializerOptions);
}
