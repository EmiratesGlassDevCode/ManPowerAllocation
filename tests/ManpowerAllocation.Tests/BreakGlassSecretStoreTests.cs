using ManpowerAllocation.Infrastructure.BreakGlass;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Tests the app-set emergency secret store: a set/get round-trip verifies against the hasher, a
/// wrong secret fails, and configuration acts as a fallback when no file has been written.
/// </summary>
public sealed class BreakGlassSecretStoreTests
{
    private sealed class FakeEnv : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task Set_then_Get_round_trips_and_verifies()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bg-{Guid.NewGuid():N}.json");
        var store = new FileBreakGlassSecretStore(
            Options.Create(new BreakGlassOptions { SecretStorePath = path, Iterations = 210_000 }),
            new FakeClock(),
            new FakeEnv());
        try
        {
            Assert.Null(await store.GetAsync());              // nothing configured yet

            await store.SetAsync("SuperSecret123!");
            var mat = await store.GetAsync();

            Assert.NotNull(mat);
            Assert.True(BreakGlassSecretHasher.Verify("SuperSecret123!", mat!.SecretHashBase64, mat.SaltBase64, mat.Iterations));
            Assert.False(BreakGlassSecretHasher.Verify("WrongSecret!!", mat.SecretHashBase64, mat.SaltBase64, mat.Iterations));
            Assert.NotNull(mat.SetAtUtc);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Get_falls_back_to_configuration_when_no_file_present()
    {
        var salt = new byte[16];
        var hash = BreakGlassSecretHasher.Derive("ConfigSecret123", salt, 210_000);

        var store = new FileBreakGlassSecretStore(
            Options.Create(new BreakGlassOptions
            {
                SecretStorePath = Path.Combine(Path.GetTempPath(), $"bg-missing-{Guid.NewGuid():N}.json"),
                SecretHashBase64 = Convert.ToBase64String(hash),
                SaltBase64 = Convert.ToBase64String(salt),
                Iterations = 210_000
            }),
            new FakeClock(),
            new FakeEnv());

        var mat = await store.GetAsync();

        Assert.NotNull(mat);
        Assert.True(BreakGlassSecretHasher.Verify("ConfigSecret123", mat!.SecretHashBase64, mat.SaltBase64, mat.Iterations));
    }
}
