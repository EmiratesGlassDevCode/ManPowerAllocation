using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// The single admin-configurable email/SMTP configuration used to send the scheduled daily report.
/// Secrets (<see cref="PasswordProtected"/>, <see cref="ClientSecretProtected"/>) are stored
/// encrypted (ASP.NET Data Protection ciphertext), never in plaintext.
/// </summary>
public sealed class EmailSettings
{
    /// <summary>The fixed primary key; there is always exactly one row.</summary>
    public const int SingletonId = 1;

    /// <summary>Surrogate primary key; always <see cref="SingletonId"/>.</summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>Whether the scheduled daily-report email is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>How the app authenticates to the SMTP server.</summary>
    public SmtpMode Mode { get; set; } = SmtpMode.NoAuth;

    /// <summary>SMTP host name or IP address.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP port (e.g. 25, 587, 465).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Transport security for the connection.</summary>
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    /// <summary>The From address on outgoing mail.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Optional From display name.</summary>
    public string? FromName { get; set; }

    /// <summary>Username for Basic / Office 365 Basic auth.</summary>
    public string? Username { get; set; }

    /// <summary>Data Protection ciphertext of the SMTP password (Basic / Office 365 Basic).</summary>
    public string? PasswordProtected { get; set; }

    /// <summary>Entra tenant id (Office 365 modern OAuth2).</summary>
    public string? TenantId { get; set; }

    /// <summary>Entra application (client) id (Office 365 modern OAuth2).</summary>
    public string? ClientId { get; set; }

    /// <summary>Data Protection ciphertext of the client secret (Office 365 modern OAuth2).</summary>
    public string? ClientSecretProtected { get; set; }

    /// <summary>The mailbox to send as / authenticate as for Office 365 modern OAuth2.</summary>
    public string? SenderMailbox { get; set; }

    /// <summary>Recipient (To) addresses, separated by comma, semicolon or new line.</summary>
    public string Recipients { get; set; } = string.Empty;

    /// <summary>Optional CC addresses.</summary>
    public string? Cc { get; set; }

    /// <summary>Optional BCC addresses.</summary>
    public string? Bcc { get; set; }

    /// <summary>Optional Reply-To address.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Local (factory) time of day at which the daily report is sent.</summary>
    public TimeSpan SendAtLocal { get; set; } = new(10, 15, 0);

    /// <summary>Whether to attach the daily report PDF.</summary>
    public bool AttachPdf { get; set; } = true;

    /// <summary>The operational date the report was last successfully emailed (idempotency guard).</summary>
    public DateTime? LastSentOperationalDate { get; set; }

    /// <summary>When the settings were last changed (UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Entra object id of the administrator who last changed the settings.</summary>
    public string? UpdatedByObjectId { get; set; }
}
