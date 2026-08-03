namespace ManpowerAllocation.Infrastructure.BreakGlass;

/// <summary>
/// Configuration for the emergency break-glass account. Bound from the protected
/// configuration / secret store (for example Azure Key Vault) — never from the database.
/// This is where the emergency secret's hash lives, keeping the application database free
/// of any credential.
/// </summary>
public sealed class BreakGlassOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "BreakGlass";

    /// <summary>The fixed login name for the emergency account.</summary>
    public string UserName { get; set; } = "breakglass";

    /// <summary>Base64-encoded PBKDF2-derived hash of the emergency secret.</summary>
    public string SecretHashBase64 { get; set; } = string.Empty;

    /// <summary>Base64-encoded salt used when deriving <see cref="SecretHashBase64"/>.</summary>
    public string SaltBase64 { get; set; } = string.Empty;

    /// <summary>PBKDF2 iteration count used when deriving the hash.</summary>
    public int Iterations { get; set; } = 210_000;

    /// <summary>Number of hours after enabling before the account auto-disables.</summary>
    public int AutoDisableAfterHours { get; set; } = 4;

    /// <summary>Email address of the IT Head who receives break-glass alerts.</summary>
    public string? ItHeadEmail { get; set; }

    /// <summary>
    /// Optional absolute path to the file where the app-set secret hash is stored. When empty, a
    /// default under the content root's <c>App_Data</c> folder is used. The app-pool identity must
    /// be able to read and write this location. Only a hash is ever written here — never the secret.
    /// </summary>
    public string? SecretStorePath { get; set; }
}
