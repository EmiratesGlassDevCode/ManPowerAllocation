using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Infrastructure.Attendance;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Time;

/// <summary>
/// Default <see cref="IFactoryClock"/>: converts the UTC clock to the configured factory time zone
/// (<see cref="AttendanceOptions.TimeZoneId"/>), falling back to UTC if that id cannot be resolved.
/// </summary>
public sealed class FactoryClock : IFactoryClock
{
    private readonly IClock _clock;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>Initialises the clock.</summary>
    /// <param name="clock">The underlying UTC clock.</param>
    /// <param name="options">Attendance options providing the factory time-zone id.</param>
    public FactoryClock(IClock clock, IOptions<AttendanceOptions> options)
    {
        _clock = clock;
        _timeZone = ResolveTimeZone(options.Value.TimeZoneId);
    }

    /// <inheritdoc />
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc), _timeZone);

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
