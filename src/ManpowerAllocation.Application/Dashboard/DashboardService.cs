using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Attendance;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    private readonly ShiftWindowOptions _shiftWindow;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="factoryClock">Local (factory) clock used to resolve the live shift.</param>
    /// <param name="shiftWindow">Grace-window tuning, so the live-shift boundary matches the sync.</param>
    public DashboardService(IApplicationDbContext dbContext, IFactoryClock factoryClock, IOptions<ShiftWindowOptions> shiftWindow)
    {
        _dbContext = dbContext;
        _factoryClock = factoryClock;
        _shiftWindow = shiftWindow.Value;
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

        // Open each shift's window GraceMinutes early, exactly as AttendanceSyncService does, so the
        // dashboard's default shift flips at the same instant the sync (and the attendance view) do.
        // With the defaults (07:00/19:00, 60-min grace) the boundary is 06:00 / 18:00.
        var grace = TimeSpan.FromMinutes(Math.Max(0, _shiftWindow.GraceMinutes));
        var dayFrom = WrapToDay(dayStart - grace);
        var nightFrom = WrapToDay(nightStart - grace);

        var now = _factoryClock.LocalNow.TimeOfDay;

        var dayLive = dayFrom <= nightFrom
            ? now >= dayFrom && now < nightFrom
            : now >= dayFrom || now < nightFrom;

        return dayLive ? ShiftFilter.Day : ShiftFilter.Night;
    }

    /// <summary>Normalises a possibly-negative time-of-day into the [0,24h) range.</summary>
    private static TimeSpan WrapToDay(TimeSpan value)
    {
        var ticks = value.Ticks % TimeSpan.TicksPerDay;
        if (ticks < 0)
        {
            ticks += TimeSpan.TicksPerDay;
        }

        return TimeSpan.FromTicks(ticks);
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
