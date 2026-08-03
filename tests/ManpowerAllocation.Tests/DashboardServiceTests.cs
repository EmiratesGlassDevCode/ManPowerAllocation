using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Domain.Enums;
using Xunit;
using static ManpowerAllocation.Tests.TestSupport;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Tests the live-shift resolution (which the dashboards open on to avoid the pooled day+night
/// view) and a basic per-shift division rollup.
/// </summary>
public sealed class DashboardServiceTests
{
    private static DashboardService Build(ManpowerAllocation.Infrastructure.Persistence.ManpowerDbContext db, int hour) =>
        new(db, new FakeFactoryClock { LocalNow = new DateTime(2026, 1, 1, hour, 0, 0, DateTimeKind.Unspecified) });

    [Theory]
    [InlineData(10, ShiftFilter.Day)]   // mid-morning
    [InlineData(8, ShiftFilter.Day)]    // just after day start (07:00)
    [InlineData(20, ShiftFilter.Night)] // after night start (19:00)
    [InlineData(3, ShiftFilter.Night)]  // small hours
    public async Task GetLiveShiftAsync_resolves_from_default_shift_times(int hour, ShiftFilter expected)
    {
        await using var db = NewContext(); // no ShiftSettings row -> defaults 07:00 / 19:00

        var live = await Build(db, hour).GetLiveShiftAsync();

        Assert.Equal(expected, live);
    }

    [Fact]
    public async Task Division_dashboard_reports_per_shift_present_and_required()
    {
        await using var db = NewContext();
        db.Departments.Add(Dept(requiredDay: 5, requiredNight: 0));
        db.Employees.AddRange(
            Emp(1, "A", ShiftType.Day, AttendanceStatus.Present),
            Emp(2, "B", ShiftType.Day, AttendanceStatus.Present),
            Emp(3, "C", ShiftType.Day, AttendanceStatus.Absent),
            Emp(4, "D", ShiftType.Night, AttendanceStatus.Present)); // night worker excluded from Day view
        await db.SaveChangesAsync();

        var dashboard = await Build(db, 10).GetDivisionDashboardAsync(Division.Egl, ShiftFilter.Day);

        Assert.Equal(2, dashboard.Totals.Present);   // only day present
        Assert.Equal(1, dashboard.Totals.Absent);
        Assert.Equal(5, dashboard.Totals.Required);  // RequiredDay only
    }
}
