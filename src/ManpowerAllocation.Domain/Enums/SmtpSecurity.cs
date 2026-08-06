namespace ManpowerAllocation.Domain.Enums;

/// <summary>The transport security used for the SMTP connection.</summary>
public enum SmtpSecurity
{
    /// <summary>No transport encryption (plain SMTP — only for a trusted internal relay).</summary>
    None = 1,

    /// <summary>Upgrade to TLS with STARTTLS after connecting (typically port 587).</summary>
    StartTls = 2,

    /// <summary>Implicit TLS from the first byte (typically port 465).</summary>
    SslOnConnect = 3
}
