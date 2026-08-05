using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Attendance;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ManpowerAllocation.Tests.TestSupport;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Functional + edge tests for the presence rule: a check-in inside the employee's schedule window
/// marks Present, vacation is preserved, no check-in marks Absent, blank badge / supply are left
/// untouched, the previous shift is evaluated cumulatively, and an unconfigured source is a no-op.
/// No ShiftSchedule rows are seeded, so the sync uses its fallback (07:00 day start, 60-min grace):
/// a Day worker's window at 10:00 is 06:00–20:00, a Night worker's most-recent window is 18:00–08:00.
/// </summary>
public sealed class AttendanceSyncServiceTests
{
    // Fixed "now": 2026-01-01 10:00 factory-local (mid-day-shift).
    private static readonly DateTime Now = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);

    private static AttendanceSyncService Build(ManpowerDbContext db, FakePresenceProvider presence) =>
        new(db, presence, new FakeClock(), new FakeFactoryClock { LocalNow = Now },
            new AttendanceSyncStatus(), NullLogger<AttendanceSyncService>.Instance);

    private static async Task<ManpowerDbContext> Seed(params Domain.Entities.Employee[] employees)
    {
        var db = NewContext();
        db.Departments.Add(Dept());
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();
        return db;
    }

    private static FakePresenceProvider WithPunches(params BiometricPunch[] punches) =>
        new() { Punches = punches };

    [Fact]
    public async Task Check_in_inside_window_marks_present_and_clears_vacation()
    {
        await using var db = await Seed(Emp(1, "100", ShiftType.Day, AttendanceStatus.OnVacation));
        var presence = WithPunches(new BiometricPunch("100", new DateTime(2026, 1, 1, 9, 0, 0)));

        var result = await Build(db, presence).SyncAsync("test");

        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task No_check_in_preserves_supervisor_vacation()
    {
        await using var db = await Seed(Emp(1, "200", ShiftType.Day, AttendanceStatus.OnVacation));

        await Build(db, WithPunches()).SyncAsync("test");

        Assert.Equal(AttendanceStatus.OnVacation, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task No_check_in_marks_absent()
    {
        await using var db = await Seed(Emp(1, "300", ShiftType.Day, AttendanceStatus.Present));

        await Build(db, WithPunches()).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Absent, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Check_in_outside_window_does_not_mark_present()
    {
        await using var db = await Seed(Emp(1, "310", ShiftType.Day, AttendanceStatus.Present));
        // A punch at 03:00 is before the day window (06:00 with grace) — it must not count.
        var presence = WithPunches(new BiometricPunch("310", new DateTime(2026, 1, 1, 3, 0, 0)));

        await Build(db, presence).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Absent, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Blank_badge_is_left_untouched()
    {
        await using var db = await Seed(Emp(1, null, ShiftType.Day, AttendanceStatus.Present));

        await Build(db, WithPunches()).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Supply_workers_are_never_changed()
    {
        await using var db = await Seed(Emp(1, "400", ShiftType.Day, AttendanceStatus.Present, supply: true));

        await Build(db, WithPunches()).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Night_worker_is_evaluated_against_the_previous_night_window()
    {
        // It is 10:00. A night worker who punched at 19:30 the previous evening must show Present.
        await using var db = await Seed(Emp(1, "500", ShiftType.Night, AttendanceStatus.Absent));
        var presence = WithPunches(new BiometricPunch("500", new DateTime(2025, 12, 31, 19, 30, 0)));

        await Build(db, presence).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Unconfigured_source_is_a_safe_no_op()
    {
        await using var db = await Seed(Emp(1, "600", ShiftType.Day, AttendanceStatus.Present));
        var presence = new FakePresenceProvider { IsConfigured = false };

        var result = await Build(db, presence).SyncAsync("test");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }
}
