using Microsoft.Extensions.Configuration;

namespace ManpowerAllocation.Web.Common;

/// <summary>
/// Converts stored UTC timestamps to the configured local (factory) time zone for display.
/// The application persists everything in UTC; screens use this so operators see local time
/// rather than GMT. The zone is taken from <c>Attendance:TimeZoneId</c> (default Arabian
/// Standard Time) and falls back to UTC if that id cannot be resolved on the host.
/// </summary>
public sealed class DisplayTimeZone
{
    private readonly TimeZoneInfo _timeZone;

    /// <summary>Resolves the display time zone from configuration.</summary>
    /// <param name="configuration">Application configuration.</param>
    public DisplayTimeZone(IConfiguration configuration)
    {
        var id = configuration["Attendance:TimeZoneId"];
        try
        {
            _timeZone = string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    /// <summary>A short label for the active display zone (its id), shown next to times.</summary>
    public string Name => _timeZone.Id;

    /// <summary>Converts a UTC instant to the display time zone.</summary>
    /// <param name="utc">The UTC timestamp.</param>
    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);

    /// <summary>Formats a UTC timestamp in the display time zone, or a dash when null.</summary>
    /// <param name="utc">The UTC timestamp, or null.</param>
    /// <param name="format">The .NET date/time format string.</param>
    public string Format(DateTime? utc, string format = "yyyy-MM-dd HH:mm") =>
        utc.HasValue ? ToLocal(utc.Value).ToString(format) : "—";
}
