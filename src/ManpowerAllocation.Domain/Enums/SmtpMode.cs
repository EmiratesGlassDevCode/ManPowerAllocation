namespace ManpowerAllocation.Domain.Enums;

/// <summary>How the application authenticates to the SMTP server when sending mail.</summary>
public enum SmtpMode
{
    /// <summary>Local relay that accepts by IP; no credentials are sent.</summary>
    NoAuth = 1,

    /// <summary>Generic SMTP AUTH with a username and password.</summary>
    Basic = 2,

    /// <summary>Office 365 with a mailbox username and app password (SMTP AUTH over STARTTLS).</summary>
    Office365Basic = 3,

    /// <summary>Office 365 with modern auth (OAuth2 client credentials, XOAUTH2).</summary>
    Office365Modern = 4
}
