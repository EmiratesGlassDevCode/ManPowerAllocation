namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Supplies the set of employees physically present today, sourced from the external
/// attendance system (a read-only view in a separate database). Implemented in the
/// infrastructure layer; the application layer never talks to the attendance database directly.
/// </summary>
public interface IPresenceProvider
{
    /// <summary>
    /// True when an attendance source is configured. When false the sync is a no-op, so a
    /// missing configuration can never cause every employee to be marked absent by accident.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Returns the badge / employee identifiers that have a check-in for today (in the
    /// configured factory time zone). Identifiers are trimmed; comparison is case-insensitive.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlySet<string>> GetPresentEmployeeIdsForTodayAsync(CancellationToken cancellationToken = default);
}
