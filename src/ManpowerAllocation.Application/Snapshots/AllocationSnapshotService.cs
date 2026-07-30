using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Snapshots;

/// <summary>
/// Default implementation of <see cref="IAllocationSnapshotService"/>. Department figures are taken
/// from <see cref="IDashboardService"/> (the same calculation the live report uses) and the
/// per-employee lines are read straight from the roster, filtered to the snapshot's shift.
/// </summary>
public sealed class AllocationSnapshotService : IAllocationSnapshotService
{
    private static readonly Division[] Divisions = { Division.Egl, Division.FunctionalSupport, Division.Brg };

    private readonly IApplicationDbContext _dbContext;
    private readonly IDashboardService _dashboard;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="dashboard">The dashboard calculation used for the department figures.</param>
    /// <param name="clock">Clock used to stamp the capture time.</param>
    public AllocationSnapshotService(IApplicationDbContext dbContext, IDashboardService dashboard, IClock clock)
    {
        _dbContext = dbContext;
        _dashboard = dashboard;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(DateTime operationalDate, ShiftType shift, CancellationToken cancellationToken = default)
    {
        var date = operationalDate.Date;
        return _dbContext.AllocationSnapshots.AnyAsync(s => s.OperationalDate == date && s.Shift == shift, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CaptureAsync(ShiftType shift, DateTime operationalDate, string source, CancellationToken cancellationToken = default)
    {
        var date = operationalDate.Date;
        var shiftFilter = shift == ShiftType.Day ? ShiftFilter.Day : ShiftFilter.Night;

        var departmentRows = new List<AllocationSnapshotDepartment>();
        int onRoll = 0, present = 0, absent = 0, onVacation = 0, supply = 0, totalPresent = 0, required = 0, shortDepts = 0;

        foreach (var division in Divisions)
        {
            var dashboard = await _dashboard.GetDivisionDashboardAsync(division, shiftFilter, cancellationToken);

            foreach (var stat in dashboard.Departments)
            {
                departmentRows.Add(new AllocationSnapshotDepartment
                {
                    DepartmentId = stat.DepartmentId,
                    Division = stat.Division,
                    DepartmentName = stat.Name,
                    IsActive = stat.IsActive,
                    Required = stat.Required,
                    OnRoll = stat.OnRoll,
                    Present = stat.Present,
                    Absent = stat.Absent,
                    OnVacation = stat.OnVacation,
                    SupplyPresent = stat.SupplyPresent,
                    TotalPresent = stat.TotalPresent,
                    Variance = stat.Variance,
                    Status = stat.Status.ToString()
                });
            }

            var totals = dashboard.Totals;
            onRoll += totals.OnRoll;
            present += totals.Present;
            absent += totals.Absent;
            onVacation += totals.OnVacation;
            supply += totals.SupplyPresent;
            totalPresent += totals.TotalPresent;
            required += totals.Required;
            shortDepts += totals.ShortageDepartmentCount;
        }

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => e.Shift == shift)
            .ToListAsync(cancellationToken);

        var employeeRows = employees
            .Select(e => new AllocationSnapshotEmployee
            {
                EmployeeId = e.Id,
                Name = e.Name,
                BadgeNumber = e.BadgeNumber,
                Division = e.Division,
                DepartmentName = e.Department?.Name ?? string.Empty,
                Shift = e.Shift,
                Status = e.Status,
                IsSupply = e.IsSupply
            })
            .ToList();

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // A re-capture (restart, or a manual re-run) replaces the previous snapshot for the
            // same date and shift. The FK cascade removes the old detail rows.
            var existing = await _dbContext.AllocationSnapshots
                .Where(s => s.OperationalDate == date && s.Shift == shift)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                _dbContext.AllocationSnapshots.RemoveRange(existing);
                await _dbContext.SaveChangesAsync(ct);
            }

            _dbContext.AllocationSnapshots.Add(new AllocationSnapshot
            {
                OperationalDate = date,
                Shift = shift,
                CapturedAtUtc = _clock.UtcNow,
                Source = source,
                OnRoll = onRoll,
                Present = present,
                Absent = absent,
                OnVacation = onVacation,
                SupplyPresent = supply,
                TotalPresent = totalPresent,
                Required = required,
                Variance = totalPresent - required,
                ShortageDepartmentCount = shortDepts,
                Departments = departmentRows,
                Employees = employeeRows
            });

            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
