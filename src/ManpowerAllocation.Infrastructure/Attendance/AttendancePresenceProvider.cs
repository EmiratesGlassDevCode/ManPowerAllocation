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

    // Labels the view uses for the shift that ran immediately before the current one. Both the
    // new ('Previous Shift') and the older ('Previous Night Shift') spellings are accepted so the
    // app keeps working whichever version of the view is deployed.
    private static readonly HashSet<string> PreviousShiftLabels =
        new(StringComparer.OrdinalIgnoreCase) { "Previous Shift", "Previous Night Shift" };

    /// <inheritdoc />
    public async Task<ShiftPresence> GetPresenceAsync(CancellationToken cancellationToken = default)
    {
        var currentShift = _options.CurrentShiftValue;

        // Pull every checked-in row (the view is already scoped to a rolling ~30h window, so this is
        // a small result set) with the same simple predicate that has always translated cleanly,
        // then bucket by shift label in memory. Doing the label split client-side avoids any
        // provider-specific translation pitfalls with an IN-list over the label column.
        var rows = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.InTime != null)
            .Select(r => new { r.EmployeeId, r.ShiftLabel })
            .ToListAsync(cancellationToken);

        var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.EmployeeId))
            {
                continue;
            }

            var id = row.EmployeeId.Trim();
            if (string.Equals(row.ShiftLabel, currentShift, StringComparison.OrdinalIgnoreCase))
            {
                current.Add(id);
            }
            else if (row.ShiftLabel is not null && PreviousShiftLabels.Contains(row.ShiftLabel))
            {
                previous.Add(id);
            }
        }

        return new ShiftPresence(current, previous);
    }
}
