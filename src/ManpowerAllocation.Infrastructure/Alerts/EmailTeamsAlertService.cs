using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using ManpowerAllocation.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Alerts;

/// <summary>
/// Sends break-glass alerts by email (SMTP) and/or Microsoft Teams (incoming webhook).
/// Whichever channels are configured are used; if none are configured the alert is logged
/// at warning level so that break-glass activity is still recorded somewhere visible.
/// </summary>
public sealed class EmailTeamsAlertService : IAlertService
{
    private readonly AlertOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailTeamsAlertService> _logger;

    /// <summary>Initialises the service.</summary>
    /// <param name="options">Alert channel configuration.</param>
    /// <param name="httpClientFactory">Factory used to create the Teams webhook client.</param>
    /// <param name="logger">Logger used for the fallback channel and diagnostics.</param>
    public EmailTeamsAlertService(
        IOptions<AlertOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<EmailTeamsAlertService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendBreakGlassEnabledAsync(string? reason, DateTime enabledAtUtc, CancellationToken cancellationToken = default)
    {
        var subject = "[Manpower Allocation] Break-glass account ENABLED";
        var body = new StringBuilder()
            .AppendLine("The emergency break-glass account has been enabled.")
            .AppendLine($"Enabled at (UTC): {enabledAtUtc:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Reason: {reason ?? "not stated"}")
            .AppendLine("The account will auto-disable four hours after enabling.")
            .ToString();

        return SendAsync(subject, body, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendBreakGlassLoginAsync(DateTime loginAtUtc, string? clientDescription, CancellationToken cancellationToken = default)
    {
        var subject = "[Manpower Allocation] Break-glass LOGIN occurred";
        var body = new StringBuilder()
            .AppendLine("A login using the emergency break-glass account has occurred.")
            .AppendLine($"Login at (UTC): {loginAtUtc:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Client: {clientDescription ?? "unknown"}")
            .ToString();

        return SendAsync(subject, body, cancellationToken);
    }

    /// <summary>Dispatches an alert over every configured channel, falling back to the log.</summary>
    private async Task SendAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var delivered = false;

        if (!string.IsNullOrWhiteSpace(_options.TeamsWebhookUrl))
        {
            await PostToTeamsAsync(subject, body, cancellationToken);
            delivered = true;
        }

        if (!string.IsNullOrWhiteSpace(_options.SmtpHost)
            && !string.IsNullOrWhiteSpace(_options.FromAddress)
            && !string.IsNullOrWhiteSpace(_options.ItHeadEmail))
        {
            await SendEmailAsync(subject, body, cancellationToken);
            delivered = true;
        }

        if (!delivered)
        {
            // No channel configured: make the event loud in the logs so it is never silent.
            _logger.LogWarning("BREAK-GLASS ALERT (no delivery channel configured): {Subject} {Body}", subject, body);
        }
    }

    /// <summary>Posts a simple message card to the configured Teams incoming webhook.</summary>
    private async Task PostToTeamsAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(EmailTeamsAlertService));
        var payload = JsonSerializer.Serialize(new { title = subject, text = body });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(_options.TeamsWebhookUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Sends the alert as an email over SMTP.</summary>
    private async Task SendEmailAsync(string subject, string body, CancellationToken cancellationToken)
    {
        using var message = new MailMessage(_options.FromAddress!, _options.ItHeadEmail!, subject, body);
        using var smtp = new SmtpClient(_options.SmtpHost!, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = CredentialCache.DefaultNetworkCredentials
        };

        await smtp.SendMailAsync(message, cancellationToken);
    }
}
