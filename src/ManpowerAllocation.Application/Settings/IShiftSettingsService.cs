namespace ManpowerAllocation.Application.Settings;

/// <summary>
/// Reads and updates the single admin-configurable shift definition (day/night start times),
/// which also drives the operational-day boundary used when deciding who is present today.
/// </summary>
public interface IShiftSettingsService
{
    /// <summary>Returns the current shift settings, falling back to the defaults if none are stored.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ShiftSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the shift settings. Requires the Admin role and is audited.</summary>
    /// <param name="request">The new shift start times.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The updated settings.</returns>
    Task<ShiftSettingsDto> UpdateAsync(UpdateShiftSettingsRequest request, CancellationToken cancellationToken = default);
}
