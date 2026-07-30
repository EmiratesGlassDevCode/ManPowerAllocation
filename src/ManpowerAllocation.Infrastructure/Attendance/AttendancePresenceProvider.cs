using ManpowerAllocation.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// Reads the external attendance view and returns the employees on the current shift. The view
/// itself buckets each punch into a shift and labels the live one (see
/// <see cref="AttendanceOptions.ShiftLabelColumn"/> / <see cref="AttendanceOptions.CurrentShiftValue"/>),
/// so "present" means a row for the current shift with a non-null check-in time. The view owns the
/// shift-boundary and early-arrival window; the application trusts its labelling rather than doing
/// its own date arithmetic, and multiple punches by one employee collapse to a single badge.
/// </summary>
public sealed class AttendancePresenceProvider : IPresenceProvider
{
    private readonly AttendanceReadDbContext _dbContext;
    private readonly AttendanceOptions _options;

    /// <summary>Initialises the provider.</summary>
    /// <param name="dbContext">The read-only attendance context.</param>
    /// <param name="options">Attendance options (the shift label column and current-shift value).</param>
    public AttendancePresenceProvider(
        AttendanceReadDbContext dbContext,
        IOptions<AttendanceOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetPresentEmployeeIdsForTodayAsync(CancellationToken cancellationToken = default)
    {
        var currentShift = _options.CurrentShiftValue;

        var ids = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.ShiftLabel == currentShift && r.InTime != null)
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
}
