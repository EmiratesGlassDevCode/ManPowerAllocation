namespace ManpowerAllocation.Application.Email;

/// <summary>A file attached to an outgoing email.</summary>
/// <param name="FileName">Suggested attachment file name.</param>
/// <param name="ContentType">MIME type (e.g. application/pdf).</param>
/// <param name="Content">The file bytes.</param>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>An email to send, using the saved SMTP settings for the server, From and recipients.</summary>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The plain-text body.</param>
/// <param name="Attachments">Optional attachments.</param>
public sealed record OutgoingEmail(string Subject, string Body, IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>
/// Sends email using the current saved <c>EmailSettings</c> (server, security, auth mode, From and
/// recipients). Implemented in the infrastructure layer over MailKit. Throws on failure so callers
/// can report or retry.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends the supplied email to the configured recipients.</summary>
    /// <param name="email">The email to send.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default);
}
