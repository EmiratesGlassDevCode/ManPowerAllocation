using System.Security.Cryptography;
using System.Text;

namespace ManpowerAllocation.Infrastructure.BreakGlass;

/// <summary>
/// Derives and verifies the emergency secret using PBKDF2 (SHA-256). The verification is
/// constant-time to avoid leaking information through timing. Note this hashes the
/// break-glass secret held in configuration; no user password is ever hashed or stored,
/// because normal authentication is performed entirely by Entra ID.
/// </summary>
public static class BreakGlassSecretHasher
{
    private const int KeyLengthBytes = 32;

    /// <summary>
    /// Derives a hash for the supplied secret and salt. Provided so an administrator can
    /// generate the configuration values offline; it is not called during normal operation.
    /// </summary>
    /// <param name="secret">The plain emergency secret.</param>
    /// <param name="salt">The salt bytes.</param>
    /// <param name="iterations">The PBKDF2 iteration count.</param>
    /// <returns>The derived hash bytes.</returns>
    public static byte[] Derive(string secret, byte[] salt, int iterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentNullException.ThrowIfNull(salt);

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeyLengthBytes);
    }

    /// <summary>
    /// Verifies a supplied secret against the configured base64 hash and salt using a
    /// constant-time comparison. Returns false if the configuration is incomplete, so an
    /// unconfigured emergency account can never be logged into.
    /// </summary>
    /// <param name="secret">The supplied secret to verify.</param>
    /// <param name="expectedHashBase64">The configured base64 hash.</param>
    /// <param name="saltBase64">The configured base64 salt.</param>
    /// <param name="iterations">The PBKDF2 iteration count.</param>
    /// <returns>True when the secret matches; otherwise false.</returns>
    public static bool Verify(string secret, string expectedHashBase64, string saltBase64, int iterations)
    {
        if (string.IsNullOrEmpty(secret)
            || string.IsNullOrEmpty(expectedHashBase64)
            || string.IsNullOrEmpty(saltBase64)
            || iterations <= 0)
        {
            return false;
        }

        byte[] expected;
        byte[] salt;
        try
        {
            expected = Convert.FromBase64String(expectedHashBase64);
            salt = Convert.FromBase64String(saltBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
