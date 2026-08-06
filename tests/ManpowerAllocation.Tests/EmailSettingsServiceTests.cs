using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Email;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure.Persistence;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>Tests for the email/SMTP settings service: secret encryption, keep-when-blank and masking.</summary>
public sealed class EmailSettingsServiceTests
{
    [Fact]
    public async Task UpdateAsync_encrypts_new_secret_and_never_returns_it()
    {
        using var context = TestSupport.NewContext();
        var protector = new ReversibleProtector();
        var service = NewService(context, protector);

        var dto = await service.UpdateAsync(BasicRequest() with { Password = "s3cret" });

        // The DTO exposes only a "configured" flag, never the secret.
        Assert.True(dto.PasswordConfigured);

        // The stored value is ciphertext, not plaintext.
        var stored = context.EmailSettings.Single();
        Assert.NotNull(stored.PasswordProtected);
        Assert.NotEqual("s3cret", stored.PasswordProtected);
        Assert.Equal("s3cret", protector.Unprotect(stored.PasswordProtected!));
    }

    [Fact]
    public async Task UpdateAsync_keeps_existing_secret_when_password_blank()
    {
        using var context = TestSupport.NewContext();
        var protector = new ReversibleProtector();
        var service = NewService(context, protector);

        await service.UpdateAsync(BasicRequest() with { Password = "s3cret" });
        var firstCipher = context.EmailSettings.Single().PasswordProtected;

        // A follow-up save with a blank password must not disturb the stored secret.
        await service.UpdateAsync(BasicRequest() with { Password = null, FromName = "Changed" });

        var stored = context.EmailSettings.Single();
        Assert.Equal(firstCipher, stored.PasswordProtected);
        Assert.Equal("Changed", stored.FromName);
    }

    [Fact]
    public async Task UpdateAsync_clears_secret_when_flag_set()
    {
        using var context = TestSupport.NewContext();
        var protector = new ReversibleProtector();
        var service = NewService(context, protector);

        await service.UpdateAsync(BasicRequest() with { Password = "s3cret" });
        await service.UpdateAsync(BasicRequest() with { ClearPassword = true });

        var stored = context.EmailSettings.Single();
        Assert.Null(stored.PasswordProtected);
    }

    [Fact]
    public async Task GetAsync_masks_secrets()
    {
        using var context = TestSupport.NewContext();
        var protector = new ReversibleProtector();
        var service = NewService(context, protector);

        await service.UpdateAsync(BasicRequest() with { Password = "s3cret" });

        var dto = await service.GetAsync();
        Assert.True(dto.PasswordConfigured);
        Assert.False(dto.ClientSecretConfigured);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_recipients()
    {
        using var context = TestSupport.NewContext();
        var service = NewService(context, new ReversibleProtector());

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.UpdateAsync(BasicRequest() with { Recipients = "   " }));
    }

    private static EmailSettingsService NewService(ManpowerDbContext context, ISecretProtector protector) =>
        new(context, new NullAuditWriter(), new AdminUser(), protector, new NullEmailSender(), new FakeClock());

    private static UpdateEmailSettingsRequest BasicRequest() => new()
    {
        Enabled = true,
        Mode = SmtpMode.Basic,
        Host = "10.0.0.1",
        Port = 587,
        Security = SmtpSecurity.StartTls,
        FromAddress = "reports@egl.local",
        Username = "smtp-user",
        Recipients = "a@egl.local, b@egl.local"
    };

    /// <summary>Reversible stand-in for Data Protection so ciphertext round-trips deterministically.</summary>
    private sealed class ReversibleProtector : ISecretProtector
    {
        private const string Prefix = "enc:";
        public string Protect(string plaintext) => Prefix + plaintext;
        public string Unprotect(string ciphertext) =>
            ciphertext.StartsWith(Prefix, StringComparison.Ordinal) ? ciphertext[Prefix.Length..] : ciphertext;
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue) { }
    }

    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AdminUser : ICurrentUser
    {
        public string UserId => "test-admin";
        public string? DisplayName => "Test Admin";
        public bool IsAuthenticated => true;
        public bool IsBreakGlassSession => false;
        public UserRole Role => UserRole.Admin;
        public bool HasAtLeast(UserRole minimumRole) => true;
    }
}
