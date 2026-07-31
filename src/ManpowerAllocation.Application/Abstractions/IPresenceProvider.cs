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
}

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
