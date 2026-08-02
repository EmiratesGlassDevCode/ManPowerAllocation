using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Dashboard;

/// <summary>
/// Default implementation of <see cref="IDashboardService"/>. All figures are computed
/// from the governed database through the pure <see cref="StaffingCalculator"/>; this
/// service performs no writes and therefore produces no audit entries.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IFactoryClock _factoryClock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="factoryClock">Local (factory) clock used to resolve the live shift.</param>
    public DashboardService(IApplicationDbContext dbContext, IFactoryClock factoryClock)
    {
        _dbContext = dbContext;
        _factoryClock = factoryClock;
    }

    /// <inheritdoc />
    public async Task<DivisionDashboard> GetDivisionDashboardAsync(Division division, ShiftFilter shift, CancellationToken cancellationToken = default)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.Division == division)
            .OrderBy(d => d.Sequence)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Division == division)
            .ToListAsync(cancellationToken);

        var byDepartment = employees
            .GroupBy(e => e.DepartmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var stats = departments
            .Select(d => StaffingCalculator.ComputeDepartmentStats(
                d,
                byDepartment.TryGetValue(d.Id, out var members) ? members : Enumerable.Empty<Employee>(),
                shift))
            .ToList();

        var totals = StaffingCalculator.RollUp(division, stats);
        return new DivisionDashboard(totals, stats);
    }

    /// <inheritdoc />
    public async Task<FactorySummary> GetFactorySummaryAsync(ShiftFilter shift, CancellationToken cancellationToken = default)
    {
        var perDivision = new List<DivisionTotals>();
        foreach (var division in Enum.GetValues<Division>())
        {
            var dashboard = await GetDivisionDashboardAsync(division, shift, cancellationToken);
            perDivision.Add(dashboard.Totals);
        }

        var factoryTotal = CombineFactoryTotal(perDivision);
        return new FactorySummary(perDivision, factoryTotal);
    }

    /// <inheritdoc />
    public async Task<ShiftFilter> GetLiveShiftAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.ShiftSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ShiftSetting.SingletonId, cancellationToken);

        var dayStart = settings?.DayShiftStart ?? new TimeSpan(7, 0, 0);
        var nightStart = settings?.NightShiftStart ?? new TimeSpan(19, 0, 0);

        var now = _factoryClock.LocalNow.TimeOfDay;

        // Day runs from dayStart until nightStart; the remainder of the 24h cycle is night. The
        // comparison handles a night start that wraps past midnight relative to the day start.
        var dayLive = dayStart <= nightStart
            ? now >= dayStart && now < nightStart
            : now >= dayStart || now < nightStart;

        return dayLive ? ShiftFilter.Day : ShiftFilter.Night;
    }

    /// <summary>
    /// Sums the per-division totals into a single factory-wide total. The <see cref="DivisionTotals.Division"/>
    /// field on the returned value is not meaningful for the combined row and is left as the first division.
    /// </summary>
    private static DivisionTotals CombineFactoryTotal(IReadOnlyCollection<DivisionTotals> divisions)
    {
        return new DivisionTotals(
            Division.Egl,
            divisions.Sum(d => d.OnRoll),
            divisions.Sum(d => d.Present),
            divisions.Sum(d => d.Absent),
            divisions.Sum(d => d.OnVacation),
            divisions.Sum(d => d.SupplyPresent),
            divisions.Sum(d => d.TotalPresent),
            divisions.Sum(d => d.Required),
            divisions.Sum(d => d.TotalPresent) - divisions.Sum(d => d.Required),
            divisions.Sum(d => d.ShortageDepartmentCount));
    }
}
