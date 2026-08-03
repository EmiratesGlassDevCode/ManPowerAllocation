using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.BreakGlass;

/// <summary>
/// Default <see cref="IBreakGlassService"/>. Deliberately exposes no way to enable the
/// account — enabling is a manual database action by IT. This service only verifies logins,
/// reports status, and enforces the four-hour auto-disable window. Every meaningful event
/// (enable observed, login, auto-disable) is written to the audit trail with the
/// break-glass flag set and an alert sent to the IT Head.
/// </summary>
public sealed class BreakGlassService : IBreakGlassService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IAlertService _alertService;
    private readonly IBreakGlassSecretStore _secretStore;
    private readonly ICurrentUser _currentUser;
    private readonly BreakGlassOptions _options;
    private readonly ILogger<BreakGlassService> _logger;

    // The emergency secret must be at least this long when set from the UI.
    private const int MinimumSecretLength = 12;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="clock">Clock used for timestamps and the auto-disable window.</param>
    /// <param name="alertService">Service used to alert the IT Head.</param>
    /// <param name="secretStore">Store for the emergency secret hash (outside the database).</param>
    /// <param name="currentUser">The current actor, used to attribute a secret change in the audit trail.</param>
    /// <param name="options">Break-glass configuration (window, alert recipient, store path).</param>
    /// <param name="logger">Logger for structured, non-sensitive diagnostics.</param>
    public BreakGlassService(
        IApplicationDbContext dbContext,
        IClock clock,
        IAlertService alertService,
        IBreakGlassSecretStore secretStore,
        ICurrentUser currentUser,
        IOptions<BreakGlassOptions> options,
        ILogger<BreakGlassService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _alertService = alertService;
        _secretStore = secretStore;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BreakGlassStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var secret = await _secretStore.GetAsync(cancellationToken);

        var account = await _dbContext.BreakGlassAccounts.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return new BreakGlassStatusDto(false, null, null, null, null, secret is not null, secret?.SetAtUtc);
        }

        var autoDisableAt = account.EnabledAtUtc?.AddHours(_options.AutoDisableAfterHours);
        return new BreakGlassStatusDto(
            account.IsEnabled,
            account.EnabledAtUtc,
            autoDisableAt,
            account.LastLoginAtUtc,
            account.EnableReason,
            secret is not null,
            secret?.SetAtUtc);
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string plainSecret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainSecret) || plainSecret.Length < MinimumSecretLength)
        {
            throw new ArgumentException(
                $"The emergency secret must be at least {MinimumSecretLength} characters.", nameof(plainSecret));
        }

        await _secretStore.SetAsync(plainSecret, cancellationToken);

        // Audit the change under the acting administrator — never log or store the secret itself.
        var now = _clock.UtcNow;
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : "system",
            UserDisplayName = _currentUser.DisplayName,
            TimestampUtc = now,
            Action = AuditAction.Update,
            EntityName = "BreakGlassSecret",
            RecordId = null,
            OldValue = null,
            NewValue = "Emergency break-glass secret set/rotated.",
            IsBreakGlassSession = _currentUser.IsBreakGlassSession
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Break-glass emergency secret was set/rotated by {Actor}.",
            _currentUser.IsAuthenticated ? _currentUser.UserId : "system");
    }

    /// <inheritdoc />
    public async Task<bool> TryAuthenticateAsync(string userName, string password, string? clientDescription, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.BreakGlassAccounts.FirstOrDefaultAsync(cancellationToken);
        if (account is null || !account.IsEnabled)
        {
            return false;
        }

        if (!string.Equals(userName, account.UserName, StringComparison.Ordinal))
        {
            return false;
        }

        // Verify against the stored hash (app-set file, else configuration) — never against anything
        // in the database. A missing secret fails closed: the account cannot be logged into.
        var secret = await _secretStore.GetAsync(cancellationToken);
        if (secret is null
            || !BreakGlassSecretHasher.Verify(password, secret.SecretHashBase64, secret.SaltBase64, secret.Iterations))
        {
            _logger.LogWarning("Rejected break-glass login attempt: secret missing or did not match.");
            return false;
        }

        var now = _clock.UtcNow;

        // First use after a manual enable starts the four-hour countdown.
        account.EnabledAtUtc ??= now;

        // Enforce the window at the point of login as well as in the background worker.
        if (now >= account.EnabledAtUtc.Value.AddHours(_options.AutoDisableAfterHours))
        {
            await DisableAsync(account, now, "Auto-disabled: four-hour window elapsed (checked at login).", cancellationToken);
            return false;
        }

        account.LastLoginAtUtc = now;
        WriteAudit(AuditAction.Update, "BreakGlassLogin", account.Id.ToString(),
            $"Break-glass login accepted from {clientDescription ?? "unknown client"}.", now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Alerts are best-effort: a failure to notify must be logged, never swallowed, and
        // must not block an emergency login when Entra ID is already down.
        await SafeAlertAsync(() => _alertService.SendBreakGlassLoginAsync(now, clientDescription, cancellationToken));

        return true;
    }

    /// <inheritdoc />
    public async Task ProcessLifecycleAsync(CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.BreakGlassAccounts.FirstOrDefaultAsync(cancellationToken);
        if (account is null || !account.IsEnabled)
        {
            return;
        }

        var now = _clock.UtcNow;

        if (account.EnabledAtUtc is null)
        {
            // A manual enable has just been observed: stamp the start time, alert, and audit.
            account.EnabledAtUtc = now;
            WriteAudit(AuditAction.Update, "BreakGlassEnabled", account.Id.ToString(),
                $"Break-glass account enabled (reason: {account.EnableReason ?? "not stated"}).", now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await SafeAlertAsync(() => _alertService.SendBreakGlassEnabledAsync(account.EnableReason, now, cancellationToken));
            return;
        }

        if (now >= account.EnabledAtUtc.Value.AddHours(_options.AutoDisableAfterHours))
        {
            await DisableAsync(account, now, "Auto-disabled: four-hour window elapsed.", cancellationToken);
        }
    }

    /// <summary>Disables the account, records the reason and audits the change.</summary>
    private async Task DisableAsync(BreakGlassAccount account, DateTime now, string reason, CancellationToken cancellationToken)
    {
        account.IsEnabled = false;
        account.DisabledAtUtc = now;
        // Reset the start time so a later manual re-enable is treated as a fresh activation.
        account.EnabledAtUtc = null;

        WriteAudit(AuditAction.Update, "BreakGlassDisabled", account.Id.ToString(), reason, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Break-glass account disabled: {Reason}", reason);
    }

    /// <summary>Writes a break-glass audit entry directly, with the actor set to the emergency account.</summary>
    private void WriteAudit(AuditAction action, string entityName, string recordId, string description, DateTime now)
    {
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = _options.UserName,
            UserDisplayName = "Break-glass emergency account",
            TimestampUtc = now,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = null,
            NewValue = description,
            IsBreakGlassSession = true
        });
    }

    /// <summary>Runs an alert action, logging (but not throwing) if it fails.</summary>
    private async Task SafeAlertAsync(Func<Task> alert)
    {
        try
        {
            await alert();
        }
        catch (Exception ex)
        {
            // Never let alerting failure block the emergency path, but never let it be silent either.
            _logger.LogError(ex, "Failed to deliver a break-glass alert to the IT Head.");
        }
    }
}
