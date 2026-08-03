using ManpowerAllocation.Application.Attendance;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static ManpowerAllocation.Tests.TestSupport;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Functional + edge tests for the attendance presence rule: check-in → Present, vacation preserved,
/// no check-in → Absent, blank badge untouched, supply untouched, cumulative per-shift evaluation,
/// and the safe no-op when no source is configured.
/// </summary>
public sealed class AttendanceSyncServiceTests
{
    private static AttendanceSyncService Build(ManpowerDbContext db, FakePresenceProvider presence)
    {
        // No ShiftSettings row → defaults (day 07:00, night 19:00); factory time 10:00 → day is live.
        return new AttendanceSyncService(
            db,
            presence,
            new FakeClock(),
            new FakeFactoryClock { LocalNow = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified) },
            Options.Create(new ShiftWindowOptions()),
            new AttendanceSyncStatus(),
            NullLogger<AttendanceSyncService>.Instance);
    }

    private static async Task<ManpowerDbContext> Seed(params Domain.Entities.Employee[] employees)
    {
        var db = NewContext();
        db.Departments.Add(Dept());
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Check_in_marks_present_and_clears_vacation()
    {
        await using var db = await Seed(Emp(1, "100", ShiftType.Day, AttendanceStatus.OnVacation));
        var presence = new FakePresenceProvider { Presence = Presence(new[] { "100" }) };

        var result = await Build(db, presence).SyncAsync("test");

        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task No_check_in_preserves_supervisor_vacation()
    {
        await using var db = await Seed(Emp(1, "200", ShiftType.Day, AttendanceStatus.OnVacation));
        var presence = new FakePresenceProvider { Presence = Presence(Array.Empty<string>()) };

        await Build(db, presence).SyncAsync("test");

        Assert.Equal(AttendanceStatus.OnVacation, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task No_check_in_marks_absent()
    {
        await using var db = await Seed(Emp(1, "300", ShiftType.Day, AttendanceStatus.Present));
        var presence = new FakePresenceProvider { Presence = Presence(Array.Empty<string>()) };

        await Build(db, presence).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Absent, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Blank_badge_is_left_untouched()
    {
        await using var db = await Seed(Emp(1, null, ShiftType.Day, AttendanceStatus.Present));
        var presence = new FakePresenceProvider { Presence = Presence(Array.Empty<string>()) };

        await Build(db, presence).SyncAsync("test");

        // A blank badge cannot be matched — it must NOT be flipped to Absent.
        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Supply_workers_are_never_changed()
    {
        await using var db = await Seed(Emp(1, "400", ShiftType.Day, AttendanceStatus.Present, supply: true));
        var presence = new FakePresenceProvider { Presence = Presence(Array.Empty<string>()) };

        await Build(db, presence).SyncAsync("test");

        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Night_worker_is_evaluated_against_the_previous_shift_when_day_is_live()
    {
        // It is 10:00 (day live). A night worker who punched last night must show Present, not Absent.
        await using var db = await Seed(Emp(1, "500", ShiftType.Night, AttendanceStatus.Absent));
        var presence = new FakePresenceProvider
        {
            Presence = Presence(current: Array.Empty<string>(), previous: new[] { "500" })
        };

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
        // Nobody is flipped to Absent just because the source is missing.
        Assert.Equal(AttendanceStatus.Present, (await db.Employees.FindAsync(1))!.Status);
    }
}
