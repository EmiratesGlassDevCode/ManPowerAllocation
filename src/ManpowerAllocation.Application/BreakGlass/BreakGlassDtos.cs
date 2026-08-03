namespace ManpowerAllocation.Application.BreakGlass;

/// <summary>
/// Read-only status of the emergency break-glass account, surfaced to administrators
/// so they can see whether it is currently active and when it will auto-disable.
/// The account can never be enabled from the UI — this is display only.
/// </summary>
public sealed record BreakGlassStatusDto(
    bool IsEnabled,
    DateTime? EnabledAtUtc,
    DateTime? AutoDisableAtUtc,
    DateTime? LastLoginAtUtc,
    string? EnableReason,
    bool SecretConfigured,
    DateTime? SecretSetAtUtc);
