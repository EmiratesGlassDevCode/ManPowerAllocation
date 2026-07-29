namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// The single emergency "break-glass" account used ONLY when Entra ID itself is
/// unreachable. This is a narrow, heavily controlled exception to the Entra-ID-only
/// authentication rule — not a parallel login system.
/// <para>
/// The account is <see cref="IsEnabled"/> = false by default and can only be enabled
/// by a manual database action performed by IT (there is no UI to enable it). It
/// auto-disables four hours after being enabled. Crucially, this row stores NO
/// credential: the break-glass secret hash lives in the protected configuration /
/// secret store, so the application database still holds no password of any kind.
/// </para>
/// </summary>
public sealed class BreakGlassAccount
{
    /// <summary>Surrogate primary key. Exactly one row exists.</summary>
    public int Id { get; set; }

    /// <summary>The fixed login identifier for the emergency account (e.g. "breakglass").</summary>
    public string UserName { get; set; } = "breakglass";

    /// <summary>
    /// Whether the account is currently usable. Disabled by default. Enabling requires a
    /// manual database change by IT; it can never be flipped on from the application UI.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// UTC timestamp the account was last enabled. Used to auto-disable the account four
    /// hours later. Null when the account has never been enabled.
    /// </summary>
    public DateTime? EnabledAtUtc { get; set; }

    /// <summary>UTC timestamp the account was last disabled (manually or by auto-expiry).</summary>
    public DateTime? DisabledAtUtc { get; set; }

    /// <summary>UTC timestamp of the most recent successful break-glass login, for review.</summary>
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>Free-text reason IT records when enabling the account.</summary>
    public string? EnableReason { get; set; }

    /// <summary>Optimistic-concurrency token so concurrent enable/disable operations cannot race.</summary>
    public byte[]? RowVersion { get; set; }
}
