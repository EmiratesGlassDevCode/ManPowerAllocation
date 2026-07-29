using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// Reads today's check-ins from the external attendance view. "Present" means a row exists for
/// the current operational day (in the configured factory time zone) with a non-null check-in
/// time. The operational day runs from the admin-configured day-shift start to the next day's
/// day-shift start, so a night-shift worker who clocked in the previous evening stays counted
/// until the shift rolls over the following morning.
/// </summary>
public sealed class AttendancePresenceProvider : IPresenceProvider
{
    private readonly AttendanceReadDbContext _dbContext;
    private readonly IClock _clock;
    private readonly AttendanceOptions _options;
    private readonly IShiftSettingsService _shiftSettings;
    private readonly ILogger<AttendancePresenceProvider> _logger;

    /// <summary>Initialises the provider.</summary>
    /// <param name="dbContext">The read-only attendance context.</param>
    /// <param name="clock">Clock used to determine the current instant.</param>
    /// <param name="options">Attendance options (time zone).</param>
    /// <param name="shiftSettings">Provides the admin-configured shift boundaries.</param>
    /// <param name="logger">Logger for non-sensitive diagnostics.</param>
    public AttendancePresenceProvider(
        AttendanceReadDbContext dbContext,
        IClock clock,
        IOptions<AttendanceOptions> options,
        IShiftSettingsService shiftSettings,
        ILogger<AttendancePresenceProvider> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _options = options.Value;
        _shiftSettings = shiftSettings;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetPresentEmployeeIdsForTodayAsync(CancellationToken cancellationToken = default)
    {
        var operationalDate = await ResolveOperationalDateAsync(cancellationToken);
        var start = operationalDate;
        var end = operationalDate.AddDays(1);

        var ids = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.Dt >= start && r.Dt < end && r.InTime != null)
            .Select(r => r.EmployeeId)
            .ToListAsync(cancellationToken);

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                present.Add(id.Trim());
            }
        }

        return present;
    }

    /// <summary>
    /// Resolves the current operational date in the factory time zone. Before the day-shift start
    /// time the operational day is still the previous calendar date (so an overnight night shift
    /// keeps counting until the morning roll-over). Falls back to UTC if the time zone is invalid.
    /// </summary>
    private async Task<DateTime> ResolveOperationalDateAsync(CancellationToken cancellationToken)
    {
        var settings = await _shiftSettings.GetAsync(cancellationToken);
        var dayShiftStart = settings.DayShiftStart;

        DateTime localNow;
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
            localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, timeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning(ex, "Attendance time zone '{TimeZoneId}' could not be resolved; using UTC.", _options.TimeZoneId);
            localNow = _clock.UtcNow;
        }

        return localNow.TimeOfDay >= dayShiftStart ? localNow.Date : localNow.Date.AddDays(-1);
    }
}
