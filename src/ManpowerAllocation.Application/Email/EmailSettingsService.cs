using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Email;

/// <summary>
/// Default <see cref="IEmailSettingsService"/>. Reads and updates the single email/SMTP settings
/// row; every change requires the Admin role and writes an audit entry. Secrets are encrypted with
/// <see cref="ISecretProtector"/> before being stored and are never returned to callers or written
/// to the audit trail.
/// </summary>
public sealed class EmailSettingsService : IEmailSettingsService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly ISecretProtector _protector;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal, used for server-side authorization and auditing.</param>
    /// <param name="protector">Encrypts newly supplied secrets before storage.</param>
    /// <param name="emailSender">Sends the test message using the saved settings.</param>
    /// <param name="clock">Supplies the current UTC time.</param>
    public EmailSettingsService(
        IApplicationDbContext dbContext,
        IAuditWriter auditWriter,
        ICurrentUser currentUser,
        ISecretProtector protector,
        IEmailSender emailSender,
        IClock clock)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _protector = protector;
        _emailSender = emailSender;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var settings = await _dbContext.EmailSettings.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == EmailSettings.SingletonId, cancellationToken)
            ?? new EmailSettings();

        return ToDto(settings);
    }

    /// <inheritdoc />
    public async Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(UserRole.Admin);
        Validate(request);

        var settings = await _dbContext.EmailSettings
            .FirstOrDefaultAsync(e => e.Id == EmailSettings.SingletonId, cancellationToken);

        var isNew = settings is null;
        settings ??= new EmailSettings { Id = EmailSettings.SingletonId };

        var before = isNew ? null : ToDto(settings);

        settings.Enabled = request.Enabled;
        settings.Mode = request.Mode;
        settings.Host = request.Host.Trim();
        settings.Port = request.Port;
        settings.Security = request.Security;
        settings.FromAddress = request.FromAddress.Trim();
        settings.FromName = Normalize(request.FromName);
        settings.Username = Normalize(request.Username);
        settings.TenantId = Normalize(request.TenantId);
        settings.ClientId = Normalize(request.ClientId);
        settings.SenderMailbox = Normalize(request.SenderMailbox);
        settings.Recipients = request.Recipients.Trim();
        settings.Cc = Normalize(request.Cc);
        settings.Bcc = Normalize(request.Bcc);
        settings.ReplyTo = Normalize(request.ReplyTo);
        settings.SendAtLocal = request.SendAtLocal;
        settings.AttachPdf = request.AttachPdf;

        // Secrets: a Clear flag removes the stored value; a non-blank value replaces it (encrypted);
        // a blank value leaves the existing ciphertext untouched.
        if (request.ClearPassword)
        {
            settings.PasswordProtected = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.Password))
        {
            settings.PasswordProtected = _protector.Protect(request.Password);
        }

        if (request.ClearClientSecret)
        {
            settings.ClientSecretProtected = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            settings.ClientSecretProtected = _protector.Protect(request.ClientSecret);
        }

        settings.UpdatedAtUtc = _clock.UtcNow;
        settings.UpdatedByObjectId = _currentUser.UserId;

        if (isNew)
        {
            _dbContext.EmailSettings.Add(settings);
        }

        var after = ToDto(settings);

        // Audit the masked DTO — secrets are never serialised into the trail.
        _auditWriter.Add(
            isNew ? AuditAction.Create : AuditAction.Update,
            nameof(EmailSettings),
            settings.Id.ToString(),
            before,
            after);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return after;
    }

    /// <inheritdoc />
    public async Task SendTestAsync(CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var email = new OutgoingEmail(
            Subject: "Manpower Allocation — SMTP test email",
            Body:
                "This is a test email from the Manpower Allocation system.\r\n\r\n" +
                "If you received it, the SMTP settings are working correctly. No action is required.");

        await _emailSender.SendAsync(email, cancellationToken);
    }

    private static void Validate(UpdateEmailSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Host))
        {
            throw new BusinessRuleException("An SMTP host or IP address is required.");
        }

        if (request.Port is < 1 or > 65535)
        {
            throw new BusinessRuleException("The SMTP port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(request.FromAddress))
        {
            throw new BusinessRuleException("A From address is required.");
        }

        if (EmailRecipients.Parse(request.Recipients).Count == 0)
        {
            throw new BusinessRuleException("At least one recipient (To) address is required.");
        }

        if (request.SendAtLocal < TimeSpan.Zero || request.SendAtLocal >= TimeSpan.FromHours(24))
        {
            throw new BusinessRuleException("The send time must be a time of day.");
        }

        switch (request.Mode)
        {
            case SmtpMode.Basic:
            case SmtpMode.Office365Basic:
                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    throw new BusinessRuleException("A username is required for authenticated SMTP.");
                }
                break;

            case SmtpMode.Office365Modern:
                if (string.IsNullOrWhiteSpace(request.TenantId)
                    || string.IsNullOrWhiteSpace(request.ClientId)
                    || string.IsNullOrWhiteSpace(request.SenderMailbox))
                {
                    throw new BusinessRuleException("Office 365 modern auth requires a tenant id, client id and sender mailbox.");
                }
                break;
        }
    }

    private void Require(UserRole minimumRole)
    {
        if (!_currentUser.HasAtLeast(minimumRole))
        {
            throw new ForbiddenException();
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EmailSettingsDto ToDto(EmailSettings s) =>
        new(
            s.Enabled,
            s.Mode,
            s.Host,
            s.Port,
            s.Security,
            s.FromAddress,
            s.FromName,
            s.Username,
            !string.IsNullOrWhiteSpace(s.PasswordProtected),
            s.TenantId,
            s.ClientId,
            !string.IsNullOrWhiteSpace(s.ClientSecretProtected),
            s.SenderMailbox,
            s.Recipients,
            s.Cc,
            s.Bcc,
            s.ReplyTo,
            s.SendAtLocal,
            s.AttachPdf,
            s.LastSentOperationalDate,
            s.UpdatedAtUtc);
}
