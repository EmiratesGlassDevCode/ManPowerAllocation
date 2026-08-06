namespace ManpowerAllocation.Application.Email;

/// <summary>
/// Reads and updates the email/SMTP settings and sends a test message. All operations require the
/// Admin role; changes are audited and secrets are stored encrypted, never returned to callers.
/// </summary>
public interface IEmailSettingsService
{
    /// <summary>Returns the current email settings with secrets masked.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the email settings (encrypting any newly supplied secrets).</summary>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends a test email to the configured recipients using the saved settings.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SendTestAsync(CancellationToken cancellationToken = default);
}
