using ManpowerAllocation.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace ManpowerAllocation.Infrastructure.Email;

/// <summary>
/// <see cref="ISecretProtector"/> over ASP.NET Data Protection. The protector is purpose-scoped so
/// email secrets can only be decrypted by this application, and only while its Data Protection keys
/// are available (keys are persisted to the configured key folder, separate from the database).
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    /// <summary>Initialises the protector.</summary>
    /// <param name="provider">The Data Protection provider.</param>
    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ManpowerAllocation.Email.Secrets.v1");
    }

    /// <inheritdoc />
    public string Protect(string plaintext) => _protector.Protect(plaintext);

    /// <inheritdoc />
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
