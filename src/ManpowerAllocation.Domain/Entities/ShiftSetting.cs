namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// The single, admin-configurable definition of the working shifts. The day shift runs from
/// <see cref="DayShiftStart"/> to <see cref="NightShiftStart"/>, and the night shift runs from
/// <see cref="NightShiftStart"/> back to <see cref="DayShiftStart"/> the following morning.
/// The day-shift start also defines the "operational day" boundary used when deciding who is
/// present today, so a night-shift worker who clocked in the previous evening remains counted
/// until the shift rolls over the next morning.
/// </summary>
public sealed class ShiftSetting
{
    /// <summary>The single settings row always uses this fixed identifier.</summary>
    public const int SingletonId = 1;

    /// <summary>Surrogate primary key; always <see cref="SingletonId"/>.</summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>Start of the day shift and the operational-day boundary (default 07:00).</summary>
    public TimeSpan DayShiftStart { get; set; } = new(7, 0, 0);

    /// <summary>Start of the night shift (default 19:00). Also the end of the day shift.</summary>
    public TimeSpan NightShiftStart { get; set; } = new(19, 0, 0);

    /// <summary>When the settings were last changed (UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Entra object id of the administrator who last changed the settings.</summary>
    public string? UpdatedByObjectId { get; set; }
}
