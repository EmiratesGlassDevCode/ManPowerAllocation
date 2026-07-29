using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Infrastructure.BreakGlass;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations at startup and guarantees the single break-glass
/// account row exists (created disabled). It never creates any user, role or credential.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly ManpowerDbContext _dbContext;
    private readonly BreakGlassOptions _breakGlassOptions;
    private readonly ILogger<DatabaseInitializer> _logger;

    /// <summary>Initialises the initializer.</summary>
    /// <param name="dbContext">The persistence context.</param>
    /// <param name="breakGlassOptions">Break-glass configuration (used only for the account name).</param>
    /// <param name="logger">Logger for startup diagnostics.</param>
    public DatabaseInitializer(
        ManpowerDbContext dbContext,
        IOptions<BreakGlassOptions> breakGlassOptions,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _breakGlassOptions = breakGlassOptions.Value;
        _logger = logger;
    }

    /// <summary>Runs migrations and ensures baseline rows exist.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);

        var accountExists = await _dbContext.BreakGlassAccounts.AnyAsync(cancellationToken);
        if (!accountExists)
        {
            // Seeded disabled by default; it can only ever be enabled by a manual database change.
            _dbContext.BreakGlassAccounts.Add(new BreakGlassAccount
            {
                UserName = _breakGlassOptions.UserName,
                IsEnabled = false
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded the disabled break-glass account row.");
        }
    }
}
