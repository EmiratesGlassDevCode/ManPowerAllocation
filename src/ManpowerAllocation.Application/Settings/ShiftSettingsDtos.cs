namespace ManpowerAllocation.Application.Settings;

/// <summary>The current shift definition, for display and use across the application.</summary>
/// <param name="DayShiftStart">Start of the day shift and the operational-day boundary.</param>
/// <param name="NightShiftStart">Start of the night shift (also the end of the day shift).</param>
/// <param name="UpdatedAtUtc">When the settings were last changed (UTC).</param>
/// <param name="UpdatedByObjectId">Entra object id of the administrator who last changed them.</param>
public sealed record ShiftSettingsDto(
    TimeSpan DayShiftStart,
    TimeSpan NightShiftStart,
    DateTime UpdatedAtUtc,
    string? UpdatedByObjectId);

/// <summary>A request to change the shift definition.</summary>
/// <param name="DayShiftStart">New day-shift start time.</param>
/// <param name="NightShiftStart">New night-shift start time.</param>
public sealed record UpdateShiftSettingsRequest(TimeSpan DayShiftStart, TimeSpan NightShiftStart);
