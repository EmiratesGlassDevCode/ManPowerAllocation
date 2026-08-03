using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Xunit;
using static ManpowerAllocation.Tests.TestSupport;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Functional + regression tests for the pure staffing rules — including the per-shift vs pooled
/// absenteeism behaviour (F-05 / issue that day+night pooling overstated absence).
/// </summary>
public sealed class StaffingCalculatorTests
{
    // A department needing 10 by day and 8 by night, with a mixed roster:
    //   Day  : 8 present, 1 absent, 1 vacation (10 own) + 1 supply present
    //   Night: 6 present, 2 absent            (8 own)
    private static (Department Dept, List<Employee> Members) Sample()
    {
        var dept = Dept(requiredDay: 10, requiredNight: 8, active: true);
        var members = new List<Employee>();
        var id = 1;

        for (var i = 0; i < 8; i++) members.Add(Emp(id++, $"D{i}", ShiftType.Day, AttendanceStatus.Present));
        members.Add(Emp(id++, "DA", ShiftType.Day, AttendanceStatus.Absent));
        members.Add(Emp(id++, "DV", ShiftType.Day, AttendanceStatus.OnVacation));
        members.Add(Emp(id++, "DS", ShiftType.Day, AttendanceStatus.Present, supply: true));

        for (var i = 0; i < 6; i++) members.Add(Emp(id++, $"N{i}", ShiftType.Night, AttendanceStatus.Present));
        members.Add(Emp(id++, "NA1", ShiftType.Night, AttendanceStatus.Absent));
        members.Add(Emp(id++, "NA2", ShiftType.Night, AttendanceStatus.Absent));

        return (dept, members);
    }

    [Fact]
    public void Day_filter_counts_only_day_shift_and_its_requirement()
    {
        var (dept, members) = Sample();

        var s = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.Day);

        Assert.Equal(10, s.OnRoll);        // 10 own day workers (supply excluded from on-roll)
        Assert.Equal(8, s.Present);
        Assert.Equal(1, s.Absent);
        Assert.Equal(1, s.OnVacation);
        Assert.Equal(1, s.SupplyPresent);
        Assert.Equal(10, s.Required);      // RequiredDay
        Assert.Equal(9, s.TotalPresent);   // 8 present + 1 supply
        Assert.Equal(-1, s.Variance);
    }

    [Fact]
    public void Night_filter_counts_only_night_shift_and_its_requirement()
    {
        var (dept, members) = Sample();

        var s = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.Night);

        Assert.Equal(8, s.OnRoll);
        Assert.Equal(6, s.Present);
        Assert.Equal(2, s.Absent);
        Assert.Equal(8, s.Required);       // RequiredNight
        Assert.Equal(6, s.TotalPresent);
        Assert.Equal(DepartmentStaffingStatus.Short, s.Status); // variance -2 < -(8*0.1)
    }

    [Fact]
    public void All_filter_pools_both_shifts_absence_equals_sum_of_day_and_night()
    {
        var (dept, members) = Sample();

        var day = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.Day);
        var night = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.Night);
        var all = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.All);

        // Regression: the pooled "All" absent is the sum of both shifts — which is exactly why
        // absenteeism looked inflated. Per-shift figures (Day/Night) are the correct, un-pooled ones.
        Assert.Equal(day.Absent + night.Absent, all.Absent); // 1 + 2 = 3
        Assert.Equal(3, all.Absent);
        Assert.Equal(1, day.Absent);                          // per-shift is not inflated
        Assert.Equal(dept.RequiredDay + dept.RequiredNight, all.Required); // 18
        Assert.Equal(18, all.OnRoll);
    }

    [Fact]
    public void Inactive_department_is_off_regardless_of_variance()
    {
        var dept = Dept(requiredDay: 5, requiredNight: 5, active: false);
        var members = new List<Employee> { Emp(1, "X", ShiftType.Day, AttendanceStatus.Present) };

        var s = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.Day);

        Assert.Equal(DepartmentStaffingStatus.Off, s.Status);
    }

    [Fact]
    public void Excess_when_present_exceeds_requirement()
    {
        var dept = Dept(requiredDay: 1, requiredNight: 0);
        var members = new List<Employee>
        {
            Emp(1, "A", ShiftType.Day, AttendanceStatus.Present),
            Emp(2, "B", ShiftType.Day, AttendanceStatus.Present)
        };

        var s = StaffingCalculator.ComputeDepartmentStats(dept, members, ShiftFilter.Day);

        Assert.Equal(1, s.Variance);
        Assert.Equal(DepartmentStaffingStatus.Excess, s.Status);
    }

    [Fact]
    public void RollUp_excludes_off_department_requirement_but_keeps_its_present_staff()
    {
        var active = StaffingCalculator.ComputeDepartmentStats(
            Dept(1, requiredDay: 10, active: true),
            new List<Employee> { Emp(1, "A", ShiftType.Day, AttendanceStatus.Present) },
            ShiftFilter.Day);

        var off = StaffingCalculator.ComputeDepartmentStats(
            Dept(2, requiredDay: 10, active: false),
            new List<Employee> { Emp(2, "B", ShiftType.Day, AttendanceStatus.Present) },
            ShiftFilter.Day);

        var totals = StaffingCalculator.RollUp(Division.Egl, new[] { active, off });

        Assert.Equal(10, totals.Required);      // only the active department's requirement
        Assert.Equal(2, totals.TotalPresent);   // both departments' present staff
        Assert.Equal(1, totals.ShortageDepartmentCount); // the active one is short (1 of 10)
    }
}
