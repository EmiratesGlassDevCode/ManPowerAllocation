namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Reversibly encrypts and decrypts small secrets (SMTP password, OAuth client secret) so they can
/// be stored as ciphertext rather than plaintext. Implemented in the infrastructure layer over
/// ASP.NET Data Protection, whose keys are persisted separately from the database.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a plaintext secret, returning ciphertext safe to store.</summary>
    /// <param name="plaintext">The secret to protect.</param>
    string Protect(string plaintext);

    /// <summary>Decrypts ciphertext produced by <see cref="Protect"/>.</summary>
    /// <param name="ciphertext">The stored ciphertext.</param>
    string Unprotect(string ciphertext);
}
