namespace ManpowerAllocation.Infrastructure.Alerts;

/// <summary>
/// Configuration for outbound break-glass alerting. At least one channel (email or Teams)
/// should be configured; if neither is, alerts are logged loudly so the requirement that
/// break-glass activity is never silent is still met at the log level.
/// </summary>
public sealed class AlertOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Alerts";

    /// <summary>Email address of the IT Head who must receive break-glass alerts.</summary>
    public string? ItHeadEmail { get; set; }

    /// <summary>SMTP host used to send email alerts.</summary>
    public string? SmtpHost { get; set; }

    /// <summary>SMTP port. Defaults to 587 (STARTTLS).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Whether the SMTP connection uses TLS.</summary>
    public bool SmtpUseTls { get; set; } = true;

    /// <summary>The From address for alert emails.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Optional Microsoft Teams incoming-webhook URL for posting alerts to a channel.</summary>
    public string? TeamsWebhookUrl { get; set; }
}
