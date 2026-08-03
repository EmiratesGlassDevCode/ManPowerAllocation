namespace ManpowerAllocation.Application.BreakGlass;

/// <summary>
/// Governs the single emergency break-glass account. The account is enabled ONLY by a
/// manual database change performed by IT — there is no method here to enable it, by design.
/// This service verifies emergency logins, reports status, and enforces the automatic
/// four-hour disable window.
/// </summary>
public interface IBreakGlassService
{
    /// <summary>Returns the current status of the emergency account for display to administrators.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<BreakGlassStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (or rotates) the emergency secret. This is the one-time administrator action that makes
    /// break-glass usable without touching configuration files; afterwards an emergency needs only a
    /// manual database enable and later disable. The secret is hashed and stored outside the database;
    /// the change is audited. Requires the Admin role (enforced by the calling page/endpoint).
    /// </summary>
    /// <param name="plainSecret">The new emergency secret (validated for a minimum length).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SetSecretAsync(string plainSecret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an emergency login. Succeeds only when the account is enabled, still within its
    /// four-hour window, and the supplied secret matches the hash held in protected configuration
    /// (never in the database). On success the login time is recorded and an alert is sent to the
    /// IT Head. On first use after enabling, the four-hour countdown is started.
    /// </summary>
    /// <param name="userName">The supplied account name.</param>
    /// <param name="password">The supplied emergency secret.</param>
    /// <param name="clientDescription">A coarse, non-sensitive client description for the alert only.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>True when the login is accepted; otherwise false.</returns>
    Task<bool> TryAuthenticateAsync(string userName, string password, string? clientDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the account lifecycle: sends the "enabled" alert and starts the countdown the first
    /// time a manual enable is observed, and automatically disables the account once four hours have
    /// elapsed. Invoked periodically by a background worker.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task ProcessLifecycleAsync(CancellationToken cancellationToken = default);
}
