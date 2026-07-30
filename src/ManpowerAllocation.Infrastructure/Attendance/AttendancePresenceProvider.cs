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
/// until the shift rolls over the following morning. A configurable boundary grace
/// (<see cref="AttendanceOptions.BoundaryGraceMinutes"/>, default 60) softens that roll-over so
/// early arrivals and late departures around the shift change are both counted; see
/// <see cref="ResolveWindowAsync"/>.
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
        var (start, end) = await ResolveWindowAsync(cancellationToken);

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
    /// Resolves the inclusive-start / exclusive-end date window (on the view's <c>Dt</c> column)
    /// of operational days currently considered "present", in the factory time zone.
    /// <para>
    /// Each operational day D runs, for display purposes, from <c>D@dayStart − grace</c> until
    /// <c>(D+1)@dayStart + grace</c>. Consecutive days therefore overlap by twice the grace around
    /// the shift boundary, so within that window both the outgoing and incoming shifts count as
    /// present; once the grace elapses the previous day drops out on the clock, with no dependence
    /// on punch-out data. With grace = 0 this collapses to the original hard cutover exactly at the
    /// day-shift start. Falls back to UTC if the configured time zone is invalid.
    /// </para>
    /// </summary>
    private async Task<(DateTime Start, DateTime End)> ResolveWindowAsync(CancellationToken cancellationToken)
    {
        var settings = await _shiftSettings.GetAsync(cancellationToken);
        var dayShiftStart = settings.DayShiftStart;
        var grace = TimeSpan.FromMinutes(Math.Max(0, _options.BoundaryGraceMinutes));

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

        // A day-start-minus-grace grace of 1h only ever brings adjacent days into range, so the
        // previous and current calendar dates are the only candidates worth testing.
        var today = localNow.Date;
        var active = new List<DateTime>();
        foreach (var date in new[] { today.AddDays(-1), today })
        {
            var runStart = date + dayShiftStart - grace;
            var runEnd = date.AddDays(1) + dayShiftStart + grace;
            if (localNow >= runStart && localNow < runEnd)
            {
                active.Add(date);
            }
        }

        if (active.Count == 0)
        {
            // Defensive fallback (unreachable while grace >= 0): the plain hard-cutover date.
            active.Add(localNow.TimeOfDay >= dayShiftStart ? today : today.AddDays(-1));
        }

        return (active.Min(), active.Max().AddDays(1));
    }
}
