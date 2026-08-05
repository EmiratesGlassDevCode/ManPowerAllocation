using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Attendance;

/// <summary>
/// Pure, side-effect-free resolution of an employee's shift window from a schedule. A schedule is a
/// 12-hour day shift starting at <c>dayStart</c> and a 12-hour night shift twelve hours later, with a
/// symmetric grace applied before the start and after the end. Given "now" (factory-local), it picks
/// the occurrence that is currently live, or — when the employee's shift is not running now — the most
/// recent past occurrence, so the board stays cumulative (a night worker still shows last night's
/// result through the morning, and a day worker shows the day's result into the evening).
/// </summary>
public static class ShiftWindowResolver
{
    private static readonly TimeSpan ShiftLength = TimeSpan.FromHours(12);

    /// <summary>
    /// Resolves the inclusive-start / exclusive-end window (absolute factory-local instants) for the
    /// employee's shift under the given schedule.
    /// </summary>
    /// <param name="dayStart">The schedule's day-shift start time.</param>
    /// <param name="graceMinutes">Grace tolerance (minutes) applied before the start and after the end.</param>
    /// <param name="shift">The employee's shift (Day or Night).</param>
    /// <param name="localNow">The current factory-local time.</param>
    public static (DateTime From, DateTime To) Resolve(TimeSpan dayStart, int graceMinutes, ShiftType shift, DateTime localNow)
    {
        var grace = TimeSpan.FromMinutes(Math.Max(0, graceMinutes));
        var startOffset = shift == ShiftType.Day ? dayStart : dayStart + ShiftLength;
        var today = localNow.Date;

        // Prefer the occurrence that is live now (now within [start - grace, start + 12h + grace)).
        for (var d = -1; d <= 1; d++)
        {
            var start = today.AddDays(d) + startOffset;
            if (localNow >= start - grace && localNow < start + ShiftLength + grace)
            {
                return (start - grace, start + ShiftLength + grace);
            }
        }

        // Otherwise use the most recent occurrence whose start is at or before now.
        var chosen = today.AddDays(-1) + startOffset;
        for (var d = 1; d >= -1; d--)
        {
            var start = today.AddDays(d) + startOffset;
            if (start <= localNow)
            {
                chosen = start;
                break;
            }
        }

        return (chosen - grace, chosen + ShiftLength + grace);
    }

    /// <summary>
    /// Returns true when any of the supplied check-in times falls inside the employee's resolved
    /// shift window.
    /// </summary>
    /// <param name="punchInTimes">The employee's check-in times (factory-local).</param>
    /// <param name="dayStart">The schedule's day-shift start time.</param>
    /// <param name="graceMinutes">Grace tolerance (minutes).</param>
    /// <param name="shift">The employee's shift.</param>
    /// <param name="localNow">The current factory-local time.</param>
    public static bool IsPresent(
        IEnumerable<DateTime> punchInTimes, TimeSpan dayStart, int graceMinutes, ShiftType shift, DateTime localNow)
    {
        var (from, to) = Resolve(dayStart, graceMinutes, shift, localNow);
        foreach (var t in punchInTimes)
        {
            if (t >= from && t < to)
            {
                return true;
            }
        }

        return false;
    }
}
