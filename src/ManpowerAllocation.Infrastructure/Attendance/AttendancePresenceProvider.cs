using ManpowerAllocation.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// Reads today's check-ins from the external attendance view. "Present" means a row exists for
/// today's date (in the configured factory time zone) with a non-null check-in time.
/// </summary>
public sealed class AttendancePresenceProvider : IPresenceProvider
{
    private readonly AttendanceReadDbContext _dbContext;
    private readonly IClock _clock;
    private readonly AttendanceOptions _options;
    private readonly ILogger<AttendancePresenceProvider> _logger;

    /// <summary>Initialises the provider.</summary>
    /// <param name="dbContext">The read-only attendance context.</param>
    /// <param name="clock">Clock used to determine the current instant.</param>
    /// <param name="options">Attendance options (time zone).</param>
    /// <param name="logger">Logger for non-sensitive diagnostics.</param>
    public AttendancePresenceProvider(
        AttendanceReadDbContext dbContext,
        IClock clock,
        IOptions<AttendanceOptions> options,
        ILogger<AttendancePresenceProvider> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetPresentEmployeeIdsForTodayAsync(CancellationToken cancellationToken = default)
    {
        var today = ResolveLocalToday();
        var start = today;
        var end = today.AddDays(1);

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

    /// <summary>Resolves the current date in the configured factory time zone, falling back to UTC.</summary>
    private DateTime ResolveLocalToday()
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, timeZone).Date;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning(ex, "Attendance time zone '{TimeZoneId}' could not be resolved; using UTC date.", _options.TimeZoneId);
            return _clock.UtcNow.Date;
        }
    }
}
