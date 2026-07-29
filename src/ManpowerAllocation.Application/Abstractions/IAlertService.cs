namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Sends out-of-band alerts to the IT Head. Used exclusively for the break-glass
/// exception so that enabling the emergency account, and every login with it, is
/// never silent. Implemented in the infrastructure layer over email and/or Teams.
/// </summary>
public interface IAlertService
{
    /// <summary>Sends an alert that the break-glass account has been enabled.</summary>
    /// <param name="reason">The reason recorded by IT when enabling the account.</param>
    /// <param name="enabledAtUtc">When the account was enabled.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SendBreakGlassEnabledAsync(string? reason, DateTime enabledAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Sends an alert that a login using the break-glass account has occurred.</summary>
    /// <param name="loginAtUtc">When the login occurred.</param>
    /// <param name="clientDescription">A coarse, non-sensitive description of the client (e.g. remote IP), for the alert only.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SendBreakGlassLoginAsync(DateTime loginAtUtc, string? clientDescription, CancellationToken cancellationToken = default);
}
