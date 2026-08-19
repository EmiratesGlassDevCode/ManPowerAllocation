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

        var shiftSettingsExist = await _dbContext.ShiftSettings.AnyAsync(cancellationToken);
        if (!shiftSettingsExist)
        {
            // Seed the default shifts (day 07:00–19:00, night 19:00–07:00); an admin can change
            // them later from the Shift Settings screen.
            _dbContext.ShiftSettings.Add(new ShiftSetting
            {
                Id = ShiftSetting.SingletonId,
                DayShiftStart = new TimeSpan(7, 0, 0),
                NightShiftStart = new TimeSpan(19, 0, 0),
                UpdatedAtUtc = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded the default shift settings row.");
        }

        var absenceCategoriesExist = await _dbContext.AbsenceReasonCategories.AnyAsync(cancellationToken);
        if (!absenceCategoriesExist)
        {
            // Seed a sensible starter set of absence sub-categories under each fixed kind. Admins
            // can rename, re-order, add to or deactivate these from the Absence Categories screen.
            var now = DateTime.UtcNow;
            var seeds = new (Domain.Enums.AbsenceKind Kind, string Name, int Sequence)[]
            {
                (Domain.Enums.AbsenceKind.Informed, "Sick Leave", 1),
                (Domain.Enums.AbsenceKind.Informed, "Emergency Leave", 2),
                (Domain.Enums.AbsenceKind.Informed, "Business Travel", 3),
                (Domain.Enums.AbsenceKind.NotInformed, "No Call / No Show", 1),
                (Domain.Enums.AbsenceKind.NotInformed, "Unreachable", 2),
                (Domain.Enums.AbsenceKind.Vacation, "Annual Leave", 1),
                (Domain.Enums.AbsenceKind.Vacation, "Casual Leave", 2),
                (Domain.Enums.AbsenceKind.Vacation, "Public Holiday", 3)
            };

            foreach (var (kind, name, sequence) in seeds)
            {
                _dbContext.AbsenceReasonCategories.Add(new AbsenceReasonCategory
                {
                    Kind = kind,
                    Name = name,
                    Sequence = sequence,
                    IsActive = true,
                    CreatedAtUtc = now
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded the default absence reason categories.");
        }

        // One-time consistency fix: "Annual Leave" belongs under the Vacation kind (aligns with the
        // On-vacation figures shown across the dashboards). Move any Informed "Annual Leave" category
        // — and the records that reference it — to Vacation. Idempotent: only acts when such a
        // category exists and no Vacation "Annual Leave" already does (avoids a duplicate).
        var informedAnnual = await _dbContext.AbsenceReasonCategories
            .Where(c => c.Kind == Domain.Enums.AbsenceKind.Informed && c.Name == "Annual Leave")
            .ToListAsync(cancellationToken);
        if (informedAnnual.Count > 0)
        {
            var vacationAnnualExists = await _dbContext.AbsenceReasonCategories
                .AnyAsync(c => c.Kind == Domain.Enums.AbsenceKind.Vacation && c.Name == "Annual Leave", cancellationToken);
            if (!vacationAnnualExists)
            {
                foreach (var category in informedAnnual)
                {
                    category.Kind = Domain.Enums.AbsenceKind.Vacation;
                }

                var ids = informedAnnual.Select(c => c.Id).ToList();
                var records = await _dbContext.EmployeeAbsences
                    .Where(a => ids.Contains(a.CategoryId) && a.Kind == Domain.Enums.AbsenceKind.Informed)
                    .ToListAsync(cancellationToken);
                foreach (var record in records)
                {
                    record.Kind = Domain.Enums.AbsenceKind.Vacation;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Reclassified 'Annual Leave' from Informed to Vacation ({RecordCount} records updated).", records.Count);
            }
        }

        var emailSettingsExist = await _dbContext.EmailSettings.AnyAsync(cancellationToken);
        if (!emailSettingsExist)
        {
            // Seeded disabled and unconfigured; an admin sets the SMTP details and recipients from
            // the Report Email screen. No credential is stored here.
            _dbContext.EmailSettings.Add(new EmailSettings
            {
                Id = EmailSettings.SingletonId,
                Enabled = false,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded the disabled email settings row.");
        }
    }
}
