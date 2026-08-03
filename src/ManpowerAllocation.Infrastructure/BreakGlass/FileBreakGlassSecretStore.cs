using System.Text.Json;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.BreakGlass;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.BreakGlass;

/// <summary>
/// Persists the emergency secret's PBKDF2 hash to a protected JSON file on the server (never the
/// application database). When the file is absent, it falls back to the <c>BreakGlass</c>
/// configuration section so an operator can still seed the secret via configuration / Key Vault.
/// Only a non-reversible hash and its salt are written — the plain secret is never stored.
/// </summary>
public sealed class FileBreakGlassSecretStore : IBreakGlassSecretStore
{
    private const int SaltBytes = 16;
    private const int DefaultIterations = 210_000;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly BreakGlassOptions _options;
    private readonly IClock _clock;
    private readonly string _filePath;

    /// <summary>Initialises the store.</summary>
    /// <param name="options">Break-glass options (fallback secret + optional store path + iterations).</param>
    /// <param name="clock">Clock used to stamp when a secret is set.</param>
    /// <param name="environment">Host environment, used to resolve the default store path.</param>
    public FileBreakGlassSecretStore(IOptions<BreakGlassOptions> options, IClock clock, IHostEnvironment environment)
    {
        _options = options.Value;
        _clock = clock;
        _filePath = string.IsNullOrWhiteSpace(_options.SecretStorePath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "break-glass-secret.json")
            : _options.SecretStorePath!;
    }

    /// <inheritdoc />
    public async Task<BreakGlassSecretMaterial?> GetAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
                var stored = JsonSerializer.Deserialize<StoredSecret>(json);
                if (stored is { } s && !string.IsNullOrEmpty(s.SecretHashBase64) && !string.IsNullOrEmpty(s.SaltBase64))
                {
                    return new BreakGlassSecretMaterial(s.SecretHashBase64, s.SaltBase64, s.Iterations, s.SetAtUtc);
                }
            }
        }
        finally
        {
            Gate.Release();
        }

        // Fallback: a hash supplied directly through configuration / Key Vault.
        if (!string.IsNullOrEmpty(_options.SecretHashBase64) && !string.IsNullOrEmpty(_options.SaltBase64))
        {
            return new BreakGlassSecretMaterial(
                _options.SecretHashBase64,
                _options.SaltBase64,
                _options.Iterations > 0 ? _options.Iterations : DefaultIterations,
                null);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task SetAsync(string plainSecret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainSecret);

        var iterations = _options.Iterations > 0 ? _options.Iterations : DefaultIterations;
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = BreakGlassSecretHasher.Derive(plainSecret, salt, iterations);

        var stored = new StoredSecret
        {
            SecretHashBase64 = Convert.ToBase64String(hash),
            SaltBase64 = Convert.ToBase64String(salt),
            Iterations = iterations,
            SetAtUtc = _clock.UtcNow
        };

        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Write to a temp file then move into place so a crash mid-write cannot corrupt the store.
            var temp = _filePath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(stored, JsonOptions), cancellationToken);
            File.Move(temp, _filePath, overwrite: true);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>On-disk shape of the stored secret hash.</summary>
    private sealed class StoredSecret
    {
        public string SecretHashBase64 { get; set; } = string.Empty;
        public string SaltBase64 { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public DateTime? SetAtUtc { get; set; }
    }
}
