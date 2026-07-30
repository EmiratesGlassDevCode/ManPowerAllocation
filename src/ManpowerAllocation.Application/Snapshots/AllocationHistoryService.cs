using ManpowerAllocation.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Snapshots;

/// <summary>Default implementation of <see cref="IAllocationHistoryService"/>.</summary>
public sealed class AllocationHistoryService : IAllocationHistoryService
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    public AllocationHistoryService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnapshotSummaryDto>> ListAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        return await _dbContext.AllocationSnapshots
            .AsNoTracking()
            .Where(s => s.OperationalDate >= from && s.OperationalDate <= to)
            .OrderByDescending(s => s.OperationalDate)
            .ThenBy(s => s.Shift)
            .Select(s => new SnapshotSummaryDto(
                s.Id, s.OperationalDate, s.Shift, s.CapturedAtUtc, s.Source,
                s.OnRoll, s.Present, s.Absent, s.OnVacation, s.SupplyPresent,
                s.TotalPresent, s.Required, s.Variance, s.ShortageDepartmentCount))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SnapshotDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.AllocationSnapshots
            .AsNoTracking()
            .Include(s => s.Departments)
            .Include(s => s.Employees)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var summary = new SnapshotSummaryDto(
            snapshot.Id, snapshot.OperationalDate, snapshot.Shift, snapshot.CapturedAtUtc, snapshot.Source,
            snapshot.OnRoll, snapshot.Present, snapshot.Absent, snapshot.OnVacation, snapshot.SupplyPresent,
            snapshot.TotalPresent, snapshot.Required, snapshot.Variance, snapshot.ShortageDepartmentCount);

        var departments = snapshot.Departments
            .OrderBy(d => d.Division)
            .ThenBy(d => d.DepartmentName)
            .Select(d => new SnapshotDepartmentDto(
                d.DepartmentId, d.Division, d.DepartmentName, d.IsActive, d.Required, d.OnRoll,
                d.Present, d.Absent, d.OnVacation, d.SupplyPresent, d.TotalPresent, d.Variance, d.Status))
            .ToList();

        var employees = snapshot.Employees
            .OrderBy(e => e.Division)
            .ThenBy(e => e.DepartmentName)
            .ThenBy(e => e.Name)
            .Select(e => new SnapshotEmployeeDto(
                e.EmployeeId, e.Name, e.BadgeNumber, e.Division, e.DepartmentName, e.Shift, e.Status, e.IsSupply))
            .ToList();

        return new SnapshotDetailDto(summary, departments, employees);
    }
}
