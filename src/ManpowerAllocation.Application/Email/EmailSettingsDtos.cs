using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Email;

/// <summary>
/// Email/SMTP settings as shown to administrators. Secrets are never returned; the
/// <c>*Configured</c> flags indicate whether a value is stored.
/// </summary>
public sealed record EmailSettingsDto(
    bool Enabled,
    SmtpMode Mode,
    string Host,
    int Port,
    SmtpSecurity Security,
    string FromAddress,
    string? FromName,
    string? Username,
    bool PasswordConfigured,
    string? TenantId,
    string? ClientId,
    bool ClientSecretConfigured,
    string? SenderMailbox,
    string Recipients,
    string? Cc,
    string? Bcc,
    string? ReplyTo,
    TimeSpan SendAtLocal,
    bool AttachPdf,
    DateTime? LastSentOperationalDate,
    DateTime UpdatedAtUtc);

/// <summary>
/// Request to update the email settings. For secrets, a non-empty value replaces the stored one; a
/// null/blank value keeps the existing secret; the matching <c>Clear*</c> flag removes it.
/// </summary>
public sealed record UpdateEmailSettingsRequest
{
    /// <summary>Whether the scheduled daily email is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Authentication mode.</summary>
    public SmtpMode Mode { get; init; } = SmtpMode.NoAuth;

    /// <summary>SMTP host or IP.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>SMTP port.</summary>
    public int Port { get; init; } = 587;

    /// <summary>Transport security.</summary>
    public SmtpSecurity Security { get; init; } = SmtpSecurity.StartTls;

    /// <summary>From address.</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>From display name.</summary>
    public string? FromName { get; init; }

    /// <summary>Username (Basic / Office 365 Basic).</summary>
    public string? Username { get; init; }

    /// <summary>New password; blank keeps the existing one.</summary>
    public string? Password { get; init; }

    /// <summary>Remove the stored password.</summary>
    public bool ClearPassword { get; init; }

    /// <summary>Tenant id (Office 365 modern).</summary>
    public string? TenantId { get; init; }

    /// <summary>Client id (Office 365 modern).</summary>
    public string? ClientId { get; init; }

    /// <summary>New client secret; blank keeps the existing one.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>Remove the stored client secret.</summary>
    public bool ClearClientSecret { get; init; }

    /// <summary>Sender mailbox (Office 365 modern).</summary>
    public string? SenderMailbox { get; init; }

    /// <summary>Recipient (To) list.</summary>
    public string Recipients { get; init; } = string.Empty;

    /// <summary>CC list.</summary>
    public string? Cc { get; init; }

    /// <summary>BCC list.</summary>
    public string? Bcc { get; init; }

    /// <summary>Reply-To address.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Local time of day to send the daily report.</summary>
    public TimeSpan SendAtLocal { get; init; } = new(10, 15, 0);

    /// <summary>Whether to attach the daily report PDF.</summary>
    public bool AttachPdf { get; init; } = true;
}
