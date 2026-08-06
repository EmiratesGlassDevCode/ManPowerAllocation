using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Email;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using MimeKit;

namespace ManpowerAllocation.Infrastructure.Email;

/// <summary>
/// Sends email over MailKit using the saved <see cref="EmailSettings"/>. Supports an unauthenticated
/// relay, Basic auth, Office 365 Basic (mailbox + app password), and Office 365 modern OAuth2
/// (client credentials via MSAL, XOAUTH2), each over None / STARTTLS / SSL. Throws on failure.
/// </summary>
public sealed class MailKitEmailSender : IEmailSender
{
    // Scope used to acquire an Office 365 SMTP token via client credentials.
    private static readonly string[] O365SmtpScopes = { "https://outlook.office365.com/.default" };

    private readonly IApplicationDbContext _dbContext;
    private readonly ISecretProtector _protector;

    /// <summary>Initialises the sender.</summary>
    /// <param name="dbContext">The persistence context (to read the current settings).</param>
    /// <param name="protector">Used to decrypt stored secrets.</param>
    public MailKitEmailSender(IApplicationDbContext dbContext, ISecretProtector protector)
    {
        _dbContext = dbContext;
        _protector = protector;
    }

    /// <inheritdoc />
    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var settings = await _dbContext.EmailSettings.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == EmailSettings.SingletonId, cancellationToken)
            ?? throw new InvalidOperationException("Email settings are not configured.");

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            throw new InvalidOperationException("No SMTP host is configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new InvalidOperationException("No From address is configured.");
        }

        var message = BuildMessage(settings, email);

        using var client = new SmtpClient();
        var security = settings.Security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls
        };

        await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
        try
        {
            await AuthenticateAsync(client, settings, cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    private MimeMessage BuildMessage(EmailSettings settings, OutgoingEmail email)
    {
        var message = new MimeMessage();

        // For Office 365 modern auth, the From must be the mailbox we authenticate as.
        var fromAddress = settings.Mode == SmtpMode.Office365Modern && !string.IsNullOrWhiteSpace(settings.SenderMailbox)
            ? settings.SenderMailbox!
            : settings.FromAddress;
        message.From.Add(new MailboxAddress(settings.FromName ?? string.Empty, fromAddress));

        AddAddresses(message.To, settings.Recipients);
        if (message.To.Count == 0)
        {
            throw new InvalidOperationException("No recipients are configured.");
        }

        AddAddresses(message.Cc, settings.Cc);
        AddAddresses(message.Bcc, settings.Bcc);
        if (!string.IsNullOrWhiteSpace(settings.ReplyTo))
        {
            AddAddresses(message.ReplyTo, settings.ReplyTo);
        }

        message.Subject = email.Subject;

        var builder = new BodyBuilder { TextBody = email.Body };
        if (email.Attachments is not null)
        {
            foreach (var attachment in email.Attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    private async Task AuthenticateAsync(SmtpClient client, EmailSettings settings, CancellationToken cancellationToken)
    {
        switch (settings.Mode)
        {
            case SmtpMode.NoAuth:
                return;

            case SmtpMode.Basic:
            case SmtpMode.Office365Basic:
            {
                var password = string.IsNullOrWhiteSpace(settings.PasswordProtected)
                    ? string.Empty
                    : _protector.Unprotect(settings.PasswordProtected);
                await client.AuthenticateAsync(settings.Username ?? string.Empty, password, cancellationToken);
                return;
            }

            case SmtpMode.Office365Modern:
            {
                var token = await AcquireO365TokenAsync(settings, cancellationToken);
                var mailbox = settings.SenderMailbox ?? settings.Username ?? settings.FromAddress;
                var oauth2 = new SaslMechanismOAuth2(mailbox, token);
                await client.AuthenticateAsync(oauth2, cancellationToken);
                return;
            }

            default:
                throw new InvalidOperationException($"Unsupported SMTP mode '{settings.Mode}'.");
        }
    }

    private async Task<string> AcquireO365TokenAsync(EmailSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.TenantId)
            || string.IsNullOrWhiteSpace(settings.ClientId)
            || string.IsNullOrWhiteSpace(settings.ClientSecretProtected))
        {
            throw new InvalidOperationException("Office 365 modern auth requires tenant id, client id and client secret.");
        }

        var clientSecret = _protector.Unprotect(settings.ClientSecretProtected);
        var app = ConfidentialClientApplicationBuilder.Create(settings.ClientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(AzureCloudInstance.AzurePublic, settings.TenantId)
            .Build();

        var result = await app.AcquireTokenForClient(O365SmtpScopes).ExecuteAsync(cancellationToken);
        return result.AccessToken;
    }

    private static void AddAddresses(InternetAddressList list, string? raw)
    {
        foreach (var address in EmailRecipients.Parse(raw))
        {
            if (MailboxAddress.TryParse(address, out var mailbox))
            {
                list.Add(mailbox);
            }
        }
    }
}
