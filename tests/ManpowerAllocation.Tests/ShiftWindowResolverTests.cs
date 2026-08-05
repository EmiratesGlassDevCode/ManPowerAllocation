using ManpowerAllocation.Application.Attendance;
using ManpowerAllocation.Domain.Enums;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Tests the pure shift-window maths for both schedules (07:00 and 06:00 day starts) with a 1-hour
/// grace: early check-in and late check-out count, out-of-window punches don't, and a night worker is
/// measured against the previous night when viewed the next morning.
/// </summary>
public sealed class ShiftWindowResolverTests
{
    private static readonly TimeSpan Seven = new(7, 0, 0);   // Schedule B (default)
    private static readonly TimeSpan Six = new(6, 0, 0);     // Schedule A
    private const int Grace = 60;

    private static DateTime At(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Day_window_for_seven_schedule_spans_0600_to_2000_with_grace()
    {
        var (from, to) = ShiftWindowResolver.Resolve(Seven, Grace, ShiftType.Day, At(2026, 1, 1, 10));

        Assert.Equal(At(2026, 1, 1, 6), from);   // 07:00 - 1h
        Assert.Equal(At(2026, 1, 1, 20), to);    // 19:00 + 1h
    }

    [Fact]
    public void Day_window_for_six_schedule_spans_0500_to_1900_with_grace()
    {
        var (from, to) = ShiftWindowResolver.Resolve(Six, Grace, ShiftType.Day, At(2026, 1, 1, 10));

        Assert.Equal(At(2026, 1, 1, 5), from);   // 06:00 - 1h
        Assert.Equal(At(2026, 1, 1, 19), to);    // 18:00 + 1h
    }

    [Theory]
    [InlineData(6, 15, true)]    // early check-in within grace
    [InlineData(19, 30, true)]   // late check-out within grace (end 19:00 + 1h)
    [InlineData(5, 30, false)]   // before the grace window
    [InlineData(20, 30, false)]  // after the grace window
    public void Seven_schedule_day_presence_respects_the_one_hour_grace(int hour, int minute, bool expected)
    {
        var punch = At(2026, 1, 1, hour, minute);

        var present = ShiftWindowResolver.IsPresent(
            new[] { punch }, Seven, Grace, ShiftType.Day, At(2026, 1, 1, 12));

        Assert.Equal(expected, present);
    }

    [Fact]
    public void Night_worker_viewed_next_morning_matches_the_previous_night()
    {
        // Schedule B night = 19:00–07:00. Viewed at 06:00 the next morning, a 19:30 punch the prior
        // evening is still inside the (cumulative) night window [18:00, 08:00].
        var punch = At(2025, 12, 31, 19, 30);

        var present = ShiftWindowResolver.IsPresent(
            new[] { punch }, Seven, Grace, ShiftType.Night, At(2026, 1, 1, 6));

        Assert.True(present);
    }

    [Fact]
    public void Night_window_is_live_in_the_evening()
    {
        var (from, to) = ShiftWindowResolver.Resolve(Seven, Grace, ShiftType.Night, At(2026, 1, 1, 21));

        Assert.Equal(At(2026, 1, 1, 18), from);      // 19:00 - 1h
        Assert.Equal(At(2026, 1, 2, 8), to);         // 07:00 next day + 1h
    }
}
