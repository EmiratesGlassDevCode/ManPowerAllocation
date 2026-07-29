using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Dashboard;

/// <summary>
/// Pure, side-effect-free implementation of the staffing business rules extracted from
/// the source dashboard. Kept separate from persistence so the rules can be reasoned
/// about and unit-tested in isolation.
/// </summary>
public static class StaffingCalculator
{
    /// <summary>
    /// A department is only counted as "short" when its deficit exceeds this fraction of
    /// the requirement; a small shortfall within tolerance is still treated as optimal.
    /// This mirrors the original <c>variance &lt; -(req * 0.1)</c> rule.
    /// </summary>
    private const double ShortageTolerance = 0.1d;

    /// <summary>
    /// Returns the required headcount for a department under the given shift filter:
    /// the day figure, the night figure, or their sum when both shifts are shown.
    /// </summary>
    /// <param name="department">The department whose requirement is being read.</param>
    /// <param name="shift">The active shift filter.</param>
    public static int RequiredFor(Department department, ShiftFilter shift) => shift switch
    {
        ShiftFilter.Day => department.RequiredDay,
        ShiftFilter.Night => department.RequiredNight,
        _ => department.RequiredDay + department.RequiredNight
    };

    /// <summary>Returns true when the employee is on the supplied shift, honouring the "All" filter.</summary>
    private static bool MatchesShift(Employee employee, ShiftFilter shift) => shift switch
    {
        ShiftFilter.Day => employee.Shift == ShiftType.Day,
        ShiftFilter.Night => employee.Shift == ShiftType.Night,
        _ => true
    };

    /// <summary>
    /// Computes the staffing statistics for a single department. The supplied employees
    /// must already be the members of <paramref name="department"/>; only shift filtering
    /// is applied here.
    /// </summary>
    /// <param name="department">The department to evaluate.</param>
    /// <param name="departmentEmployees">The employees currently allocated to that department.</param>
    /// <param name="shift">The active shift filter.</param>
    /// <returns>The computed statistics, including the derived staffing status.</returns>
    public static DepartmentStats ComputeDepartmentStats(
        Department department,
        IEnumerable<Employee> departmentEmployees,
        ShiftFilter shift)
    {
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(departmentEmployees);

        var inShift = departmentEmployees.Where(e => MatchesShift(e, shift)).ToList();

        // Own headcount excludes outsource/supply workers; supply is tracked separately
        // because a present supply worker fills a required slot but is not "on roll".
        var own = inShift.Where(e => !e.IsSupply).ToList();
        var onRoll = own.Count;
        var present = own.Count(e => e.Status == AttendanceStatus.Present);
        var absent = own.Count(e => e.Status == AttendanceStatus.Absent);
        var onVacation = own.Count(e => e.Status == AttendanceStatus.OnVacation);
        var supplyPresent = inShift.Count(e => e.IsSupply && e.Status == AttendanceStatus.Present);

        var required = RequiredFor(department, shift);
        var totalPresent = present + supplyPresent;
        var variance = totalPresent - required;

        var status = ResolveStatus(department.IsActive, variance, required);

        return new DepartmentStats(
            department.Id,
            department.Name,
            department.Division,
            department.IsActive,
            required,
            onRoll,
            present,
            absent,
            onVacation,
            supplyPresent,
            totalPresent,
            variance,
            status);
    }

    /// <summary>
    /// Applies the staffing-status rule: an OFF department is always "Off"; otherwise a
    /// deficit beyond the tolerance band is "Short", a surplus is "Excess", and anything
    /// in between (including a small shortfall within tolerance) is "Optimal".
    /// </summary>
    private static DepartmentStaffingStatus ResolveStatus(bool isActive, int variance, int required)
    {
        if (!isActive)
        {
            return DepartmentStaffingStatus.Off;
        }

        if (variance < -(required * ShortageTolerance))
        {
            return DepartmentStaffingStatus.Short;
        }

        return variance > 0 ? DepartmentStaffingStatus.Excess : DepartmentStaffingStatus.Optimal;
    }

    /// <summary>
    /// Rolls a set of already-computed department statistics up into division totals.
    /// </summary>
    /// <param name="division">The division being summarised.</param>
    /// <param name="departmentStats">The per-department statistics for that division.</param>
    /// <returns>The aggregated division totals.</returns>
    public static DivisionTotals RollUp(Division division, IReadOnlyCollection<DepartmentStats> departmentStats)
    {
        ArgumentNullException.ThrowIfNull(departmentStats);

        // An OFF department contributes its present staff to the division's available pool but
        // NOT a requirement — otherwise switching a department off would create a phantom
        // deficit. Requirement and variance therefore only count active (non-Off) departments,
        // while present/on-roll counts include everyone.
        var required = departmentStats
            .Where(d => d.Status != DepartmentStaffingStatus.Off)
            .Sum(d => d.Required);
        var totalPresent = departmentStats.Sum(d => d.TotalPresent);

        return new DivisionTotals(
            division,
            departmentStats.Sum(d => d.OnRoll),
            departmentStats.Sum(d => d.Present),
            departmentStats.Sum(d => d.Absent),
            departmentStats.Sum(d => d.OnVacation),
            departmentStats.Sum(d => d.SupplyPresent),
            totalPresent,
            required,
            totalPresent - required,
            departmentStats.Count(d => d.Status == DepartmentStaffingStatus.Short));
    }
}
