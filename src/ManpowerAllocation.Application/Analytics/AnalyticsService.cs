using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Analytics;

/// <summary>Default <see cref="IAnalyticsService"/>. Pure read-side aggregation over captured history.</summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    public AnalyticsService(IApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OverallTrendPoint>> GetOverallTrendAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        RequireViewer();
        var (start, end) = Range(from, to);

        var rows = await _dbContext.AllocationSnapshots
            .AsNoTracking()
            .Where(s => s.OperationalDate >= start && s.OperationalDate <= end)
            .OrderByDescending(s => s.OperationalDate).ThenByDescending(s => s.Shift)
            .Select(s => new
            {
                s.OperationalDate, s.Shift, s.Required, s.TotalPresent, s.Absent,
                s.OnVacation, s.SupplyPresent, s.Variance, s.ShortageDepartmentCount
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(s => new OverallTrendPoint(
                s.OperationalDate, s.Shift.ToString(), s.Required, s.TotalPresent, s.Absent, s.OnVacation,
                s.SupplyPresent, s.Variance, Pct(s.TotalPresent, s.Required), s.ShortageDepartmentCount))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentTrendRow>> GetDepartmentTrendAsync(DateTime from, DateTime to, Division? division, CancellationToken cancellationToken = default)
    {
        RequireViewer();
        var (start, end) = Range(from, to);

        var rows = await (
            from d in _dbContext.AllocationSnapshotDepartments.AsNoTracking()
            join s in _dbContext.AllocationSnapshots.AsNoTracking() on d.SnapshotId equals s.Id
            where s.OperationalDate >= start && s.OperationalDate <= end
                && (division == null || d.Division == division)
            select new { d.DepartmentId, d.DepartmentName, d.Division, d.Required, d.TotalPresent, d.Variance })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.DepartmentId, r.DepartmentName, r.Division })
            .Select(g => new DepartmentTrendRow(
                g.Key.DepartmentId,
                g.Key.DepartmentName,
                DivisionLabel(g.Key.Division),
                g.Count(),
                Math.Round(g.Average(x => x.Required), 1),
                Math.Round(g.Average(x => x.TotalPresent), 1),
                Pct((int)Math.Round(g.Average(x => (double)x.TotalPresent)), (int)Math.Round(g.Average(x => (double)x.Required))),
                g.Count(x => x.Variance < 0),
                g.Count(x => x.Variance > 0),
                g.Min(x => x.Variance)))
            .OrderBy(r => r.Division).ThenByDescending(r => r.DaysShort).ThenBy(r => r.Department)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AbsenteeAnalyticsDto> GetAbsenteeAnalyticsAsync(DateTime from, DateTime to, Division? division, CancellationToken cancellationToken = default)
    {
        RequireViewer();
        var start = DateOnly.FromDateTime(from.Date);
        var end = DateOnly.FromDateTime(to.Date);

        // Records whose absence window intersects the selected range.
        var rows = await (
            from a in _dbContext.EmployeeAbsences.AsNoTracking()
            join e in _dbContext.Employees.AsNoTracking() on a.EmployeeId equals e.Id
            join dep in _dbContext.Departments.AsNoTracking() on e.DepartmentId equals dep.Id
            where a.FromDate <= end && (a.ToDate == null || a.ToDate >= start)
                && (division == null || e.Division == division)
            select new { a.EmployeeId, a.Kind, Category = a.Category!.Name, e.Division, Department = dep.Name })
            .ToListAsync(cancellationToken);

        var byCategory = rows
            .GroupBy(r => new { r.Kind, r.Category })
            .Select(g => new AbsenceByCategoryRow(
                g.Key.Kind == AbsenceKind.Informed ? "Informed" : "Not Informed",
                g.Key.Category,
                g.Count(),
                g.Select(x => x.EmployeeId).Distinct().Count()))
            .OrderBy(r => r.Kind).ThenByDescending(r => r.Records)
            .ToList();

        var byDepartment = rows
            .GroupBy(r => new { r.Division, r.Department })
            .Select(g => new AbsenceByDepartmentRow(
                DivisionLabel(g.Key.Division),
                g.Key.Department,
                g.Count(),
                g.Select(x => x.EmployeeId).Distinct().Count()))
            .OrderByDescending(r => r.Records).ThenBy(r => r.Department)
            .ToList();

        return new AbsenteeAnalyticsDto(
            byCategory,
            byDepartment,
            rows.Count,
            rows.Select(r => r.EmployeeId).Distinct().Count(),
            rows.Count(r => r.Kind == AbsenceKind.Informed),
            rows.Count(r => r.Kind == AbsenceKind.NotInformed));
    }

    /// <inheritdoc />
    public async Task<EmployeeHistoryDto?> GetEmployeeHistoryAsync(int employeeId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        RequireViewer();
        var (start, end) = Range(from, to);
        var startDate = DateOnly.FromDateTime(from.Date);
        var endDate = DateOnly.FromDateTime(to.Date);

        var employee = await _dbContext.Employees.AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.Id, e.Name, e.BadgeNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return null;
        }

        var days = await (
            from se in _dbContext.AllocationSnapshotEmployees.AsNoTracking()
            join s in _dbContext.AllocationSnapshots.AsNoTracking() on se.SnapshotId equals s.Id
            where se.EmployeeId == employeeId && s.OperationalDate >= start && s.OperationalDate <= end
            orderby s.OperationalDate descending, se.Shift descending
            select new { s.OperationalDate, se.Shift, se.DepartmentName, se.Status })
            .ToListAsync(cancellationToken);

        var dayRows = days
            .Select(d => new EmployeeDayStatus(d.OperationalDate, d.Shift.ToString(), d.DepartmentName, StatusLabel(d.Status)))
            .ToList();

        var absences = await _dbContext.EmployeeAbsences.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.FromDate <= endDate && (a.ToDate == null || a.ToDate >= startDate))
            .OrderByDescending(a => a.FromDate)
            .Select(a => new EmployeeAbsenceRow(
                a.Kind == AbsenceKind.Informed ? "Informed" : "Not Informed",
                a.Category!.Name, a.FromDate, a.ToDate, a.Comment, a.CreatedByName))
            .ToListAsync(cancellationToken);

        return new EmployeeHistoryDto(
            employee.Id, employee.Name, employee.BadgeNumber,
            days.Count(d => d.Status == AttendanceStatus.Present),
            days.Count(d => d.Status == AttendanceStatus.Absent),
            days.Count(d => d.Status == AttendanceStatus.OnVacation),
            dayRows, absences);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeOption>> GetEmployeeOptionsAsync(CancellationToken cancellationToken = default)
    {
        RequireViewer();

        return await (
            from e in _dbContext.Employees.AsNoTracking()
            join dep in _dbContext.Departments.AsNoTracking() on e.DepartmentId equals dep.Id
            orderby e.Name
            select new EmployeeOption(e.Id, e.Name, e.BadgeNumber, dep.Name, e.Division))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Normalises a From/To pair to inclusive date bounds (order-independent).</summary>
    private static (DateTime Start, DateTime End) Range(DateTime from, DateTime to)
    {
        var a = from.Date;
        var b = to.Date;
        return a <= b ? (a, b) : (b, a);
    }

    /// <summary>Integer fill percentage of present against required.</summary>
    private static int Pct(int present, int required) => required > 0 ? (int)Math.Round(100.0 * present / required) : 0;

    private static string StatusLabel(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.Absent => "Absent",
        AttendanceStatus.OnVacation => "On vacation",
        _ => status.ToString()
    };

    private static string DivisionLabel(Division division) => division switch
    {
        Division.Egl => "EGL",
        Division.FunctionalSupport => "Functional Support",
        Division.Brg => "BRG",
        _ => division.ToString()
    };

    private void RequireViewer()
    {
        if (!_currentUser.HasAtLeast(UserRole.Viewer))
        {
            throw new ForbiddenException("Viewing analytics requires at least the Viewer role.");
        }
    }
}
