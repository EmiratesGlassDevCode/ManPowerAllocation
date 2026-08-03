namespace ManpowerAllocation.Application.BreakGlass;

/// <summary>
/// Derived secret material for the emergency account. This is a PBKDF2 hash and its salt — never
/// the plain secret, which is not stored anywhere.
/// </summary>
/// <param name="SecretHashBase64">Base64 PBKDF2-SHA256 hash of the secret.</param>
/// <param name="SaltBase64">Base64 salt used to derive the hash.</param>
/// <param name="Iterations">PBKDF2 iteration count used.</param>
/// <param name="SetAtUtc">When the secret was set, if known.</param>
public sealed record BreakGlassSecretMaterial(string SecretHashBase64, string SaltBase64, int Iterations, DateTime? SetAtUtc);

/// <summary>
/// Stores the emergency secret's hash OUTSIDE the application database — in a protected file on the
/// server, with the <c>BreakGlass</c> configuration section as a fallback. This lets an administrator
/// set the secret once from the UI (an audited action) so that, in an emergency, the only remaining
/// steps are a manual database enable and later disable. The database never holds any credential.
/// </summary>
public interface IBreakGlassSecretStore
{
    /// <summary>Returns the current secret material, or null when none has been configured yet.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<BreakGlassSecretMaterial?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Derives a fresh salted hash from the supplied plain secret and persists it, replacing any
    /// previous value. The plain secret is never stored.
    /// </summary>
    /// <param name="plainSecret">The new emergency secret.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SetAsync(string plainSecret, CancellationToken cancellationToken = default);
}
