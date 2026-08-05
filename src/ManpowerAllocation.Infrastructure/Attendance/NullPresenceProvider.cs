using ManpowerAllocation.Application.Abstractions;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// Fallback presence provider used when no attendance database connection string is configured.
/// It reports itself as not configured so the sync becomes a safe no-op rather than marking every
/// employee absent.
/// </summary>
public sealed class NullPresenceProvider : IPresenceProvider
{
    /// <inheritdoc />
    public bool IsConfigured => false;

    /// <inheritdoc />
    public Task<ShiftPresence> GetPresenceAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ShiftPresence.Empty);

    /// <inheritdoc />
    public Task<IReadOnlyList<BiometricIdentity>> GetRecentIdentitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BiometricIdentity>>(Array.Empty<BiometricIdentity>());

    /// <inheritdoc />
    public Task<IReadOnlyList<BiometricPunch>> GetRecentPunchesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BiometricPunch>>(Array.Empty<BiometricPunch>());
}
