namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Supplies who was present for the current shift and for the immediately previous shift, sourced
/// from the external attendance view. Implemented in the infrastructure layer; the application
/// layer never talks to the attendance database directly. Two sets are returned so an employee can
/// be shown their OWN shift's most recent result — the live shift when it is running, otherwise the
/// last time their shift ran — which is what makes the board cumulative across day and night.
/// </summary>
public interface IPresenceProvider
{
    /// <summary>
    /// True when an attendance source is configured. When false the sync is a no-op, so a
    /// missing configuration can never cause every employee to be marked absent by accident.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Returns the present identifiers for the current and previous shift windows.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ShiftPresence> GetPresenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every distinct identifier the biometric source currently exposes in its window, with
    /// light metadata, so the roster can be reconciled against who is actually punching. Unlike
    /// <see cref="GetPresenceAsync"/> this is neither bucketed by shift nor filtered to "present" —
    /// it is the raw distinct set of identifiers the view holds, used to prove a real pull happened
    /// and to find punches that match no employee badge.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<BiometricIdentity>> GetRecentIdentitiesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One distinct identifier observed in the biometric attendance source, aggregated across every row
/// that carried it in the current window.
/// </summary>
/// <param name="BadgeNumber">The identifier as recorded by the attendance system (maps to <c>Employee.BadgeNumber</c>).</param>
/// <param name="ShiftLabel">The shift label on the most recent row for this identifier, if any.</param>
/// <param name="LastSeen">The most recent check-in time recorded for this identifier, as stored by the view.</param>
/// <param name="PunchCount">How many rows in the window carried this identifier.</param>
public sealed record BiometricIdentity(string BadgeNumber, string? ShiftLabel, DateTime? LastSeen, int PunchCount);

/// <summary>
/// Present employee identifiers bucketed by shift window. Identifiers are trimmed; membership tests
/// are case-insensitive.
/// </summary>
/// <param name="CurrentShift">Identifiers with a check-in in the shift that is live now.</param>
/// <param name="PreviousShift">Identifiers with a check-in in the shift that ran immediately before.</param>
public sealed record ShiftPresence(IReadOnlySet<string> CurrentShift, IReadOnlySet<string> PreviousShift)
{
    /// <summary>An empty result (nothing present), used when no source is configured.</summary>
    public static ShiftPresence Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
