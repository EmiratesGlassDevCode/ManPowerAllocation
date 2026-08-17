using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Analytics;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>Covers the analytics read-side over captured snapshots and recorded absences.</summary>
public sealed class AnalyticsServiceTests
{
    private static async Task SeedAsync(Infrastructure.Persistence.ManpowerDbContext db)
    {
        db.Departments.Add(TestSupport.Dept(id: 1, division: Division.Egl));
        db.Departments.Add(TestSupport.Dept(id: 2, division: Division.Egl));
        db.Employees.Add(TestSupport.Emp(101, "B101", ShiftType.Day, AttendanceStatus.Absent, deptId: 1));
        db.Employees.Add(TestSupport.Emp(202, "B202", ShiftType.Day, AttendanceStatus.Present, deptId: 2));

        var informed = new AbsenceReasonCategory { Kind = AbsenceKind.Informed, Name = "Annual Leave", Sequence = 1, IsActive = true, CreatedAtUtc = new DateTime(2026, 1, 1) };
        db.AbsenceReasonCategories.Add(informed);
        await db.SaveChangesAsync();

        // Two captured days (day shift), department 1 short on both, department 2 fine.
        foreach (var day in new[] { 10, 11 })
        {
            var snap = new AllocationSnapshot
            {
                OperationalDate = new DateTime(2026, 1, day),
                Shift = ShiftType.Day,
                CapturedAtUtc = new DateTime(2026, 1, day, 10, 0, 0, DateTimeKind.Utc),
                Source = "test",
                OnRoll = 20, Present = 15, Absent = 3, OnVacation = 2, SupplyPresent = 1,
                TotalPresent = 16, Required = 18, Variance = -2, ShortageDepartmentCount = 1,
                Departments =
                {
                    new AllocationSnapshotDepartment { DepartmentId = 1, Division = Division.Egl, DepartmentName = "D1", IsActive = true, Required = 10, OnRoll = 10, Present = 7, Absent = 2, OnVacation = 1, SupplyPresent = 0, TotalPresent = 7, Variance = -3, Status = "Shortage" },
                    new AllocationSnapshotDepartment { DepartmentId = 2, Division = Division.Egl, DepartmentName = "D2", IsActive = true, Required = 8, OnRoll = 10, Present = 9, Absent = 1, OnVacation = 1, SupplyPresent = 1, TotalPresent = 9, Variance = 1, Status = "Optimal" }
                },
                Employees =
                {
                    new AllocationSnapshotEmployee { EmployeeId = 101, Name = "E101", BadgeNumber = "B101", Division = Division.Egl, DepartmentName = "D1", Shift = ShiftType.Day, Status = AttendanceStatus.Absent, IsSupply = false },
                    new AllocationSnapshotEmployee { EmployeeId = 202, Name = "E202", BadgeNumber = "B202", Division = Division.Egl, DepartmentName = "D2", Shift = ShiftType.Day, Status = AttendanceStatus.Present, IsSupply = false }
                }
            };
            db.AllocationSnapshots.Add(snap);
        }

        // An informed absence for employee 101 covering the range.
        db.EmployeeAbsences.Add(new EmployeeAbsence
        {
            EmployeeId = 101, CategoryId = informed.Id, Kind = AbsenceKind.Informed,
            FromDate = new DateOnly(2026, 1, 9), ToDate = new DateOnly(2026, 1, 15),
            Comment = "leave", CreatedByObjectId = "u", CreatedByName = "Head", CreatedAtUtc = new DateTime(2026, 1, 9)
        });

        await db.SaveChangesAsync();
    }

    private static AnalyticsService NewService(Infrastructure.Persistence.ManpowerDbContext db) =>
        new(db, new FakeAnalyticsUser());

    private static readonly DateTime From = new(2026, 1, 1);
    private static readonly DateTime To = new(2026, 1, 31);

    [Fact]
    public async Task Overall_trend_returns_one_point_per_capture_with_fill()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        var points = await NewService(db).GetOverallTrendAsync(From, To);

        Assert.Equal(2, points.Count);
        // 16 present / 18 required ≈ 89%.
        Assert.Equal(89, points[0].FillPct);
        Assert.All(points, p => Assert.Equal("Day", p.Shift));
    }

    [Fact]
    public async Task Department_trend_aggregates_captures_and_shortage_days()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        var rows = await NewService(db).GetDepartmentTrendAsync(From, To, null);

        var d1 = rows.Single(r => r.DepartmentId == 1);
        Assert.Equal(2, d1.Captures);
        Assert.Equal(2, d1.DaysShort);      // short on both days
        Assert.Equal(-3, d1.WorstVariance);

        var d2 = rows.Single(r => r.DepartmentId == 2);
        Assert.Equal(0, d2.DaysShort);
        Assert.Equal(2, d2.DaysExcess);
    }

    [Fact]
    public async Task Department_trend_respects_division_filter()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        var brg = await NewService(db).GetDepartmentTrendAsync(From, To, Division.Brg);
        Assert.Empty(brg); // all seeded departments are EGL
    }

    [Fact]
    public async Task Absentee_analytics_breaks_down_by_category_and_department()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        var result = await NewService(db).GetAbsenteeAnalyticsAsync(From, To, null);

        Assert.Equal(1, result.TotalRecords);
        Assert.Equal(1, result.InformedRecords);
        Assert.Single(result.ByCategory);
        Assert.Equal("Annual Leave", result.ByCategory[0].Category);
        Assert.Single(result.ByDepartment);

        // Trend has a point per captured day (2).
        Assert.Equal(2, result.Trend.Count);
        // D1 averaged 2 absent of 10 on-roll → 20% rate.
        var d1Rate = result.ByDepartmentRate.Single(r => r.Department == "D1");
        Assert.Equal(20, d1Rate.AbsenceRatePct);
        // Employee 101 was absent on both captured days.
        var top = result.TopAbsentees.Single(t => t.EmployeeId == 101);
        Assert.Equal(2, top.AbsentDays);
    }

    [Fact]
    public async Task Employee_history_returns_days_and_absences()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        var history = await NewService(db).GetEmployeeHistoryAsync(101, From, To);

        Assert.NotNull(history);
        Assert.Equal(2, history!.Days.Count);       // two captured days
        Assert.Equal(2, history.AbsentDays);
        Assert.Single(history.Absences);
        Assert.Equal("Annual Leave", history.Absences[0].Category);
    }

    [Fact]
    public async Task Employee_history_is_null_for_unknown_employee()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        Assert.Null(await NewService(db).GetEmployeeHistoryAsync(999, From, To));
    }

    [Fact]
    public async Task Employee_options_lists_all_employees()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);

        var options = await NewService(db).GetEmployeeOptionsAsync();
        Assert.Equal(2, options.Count);
    }

    private sealed class FakeAnalyticsUser : ICurrentUser
    {
        public string UserId => "viewer";
        public string? DisplayName => "Viewer";
        public bool IsAuthenticated => true;
        public bool IsBreakGlassSession => false;
        public UserRole Role => UserRole.Viewer;
        public bool HasAtLeast(UserRole minimumRole) => (int)UserRole.Viewer >= (int)minimumRole;
    }
}
