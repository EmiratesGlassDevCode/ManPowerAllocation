using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Application.Settings;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Allocation;

/// <summary>
/// Default <see cref="IAllocationResetService"/>. A reset returns every employee whose current
/// department differs from their home department back to that home (and re-aligns their division),
/// clearing all shift loans. Enabling the automatic reset is gated on a master upload.
/// </summary>
public sealed class AllocationResetService : IAllocationResetService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;
    private readonly IFactoryClock _factoryClock;
    private readonly IShiftSettingsService _shiftSettings;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    public AllocationResetService(
        IApplicationDbContext dbContext,
        IAuditWriter auditWriter,
        IClock clock,
        IFactoryClock factoryClock,
        IShiftSettingsService shiftSettings,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _clock = clock;
        _factoryClock = factoryClock;
        _shiftSettings = shiftSettings;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<AllocationResetStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);
        var settings = await GetSettingsAsync(cancellationToken);

        var loanedNow = await _dbContext.Employees
            .CountAsync(e => e.HomeDepartmentId != null && e.DepartmentId != e.HomeDepartmentId, cancellationToken);
        var unsetHome = await _dbContext.Employees.CountAsync(e => e.HomeDepartmentId == null, cancellationToken);

        return new AllocationResetStatusDto(
            settings.AutoShiftResetEnabled,
            settings.LastMasterUploadUtc,
            settings.LastResetAtUtc,
            loanedNow,
            unsetHome,
            settings.LastMasterUploadUtc != null && unsetHome == 0);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);
        var settings = await GetSettingsAsync(cancellationToken);

        if (enabled)
        {
            if (settings.LastMasterUploadUtc is null)
            {
                throw new BusinessRuleException("Upload a master sheet before enabling the automatic shift-reset.");
            }

            var unsetHome = await _dbContext.Employees.CountAsync(e => e.HomeDepartmentId == null, cancellationToken);
            if (unsetHome > 0)
            {
                throw new BusinessRuleException($"{unsetHome} employees have no home department set — upload a master sheet first.");
            }

            // Anchor to the current boundary so enabling does not disturb the shift in progress; the
            // first automatic reset happens at the next day/night changeover.
            var shift = await _shiftSettings.GetAsync(cancellationToken);
            settings.LastResetMarker = CurrentBoundaryMarker(shift.DayShiftStart, shift.NightShiftStart);
        }

        settings.AutoShiftResetEnabled = enabled;
        settings.UpdatedAtUtc = _clock.UtcNow;
        settings.UpdatedByObjectId = _currentUser.UserId;

        _auditWriter.Add(AuditAction.Update, nameof(AllocationSettings), settings.Id.ToString(), null,
            new { AutoShiftResetEnabled = enabled });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> ResetNowAsync(CancellationToken cancellationToken = default)
    {
        Require(UserRole.User);
        var settings = await GetSettingsAsync(cancellationToken);
        var shift = await _shiftSettings.GetAsync(cancellationToken);
        return await PerformResetAsync(settings, CurrentBoundaryMarker(shift.DayShiftStart, shift.NightShiftStart), "manual", cancellationToken);
    }

    /// <inheritdoc />
    public async Task ProcessBoundaryAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.AutoShiftResetEnabled)
        {
            return;
        }

        var shift = await _shiftSettings.GetAsync(cancellationToken);
        var marker = CurrentBoundaryMarker(shift.DayShiftStart, shift.NightShiftStart);
        if (settings.LastResetMarker == marker)
        {
            return;
        }

        await PerformResetAsync(settings, marker, "auto", cancellationToken);
    }

    /// <summary>Returns all loaned employees to their home department and records the run. Returns the count reset.</summary>
    private async Task<int> PerformResetAsync(AllocationSettings settings, string marker, string trigger, CancellationToken cancellationToken)
    {
        var loaned = await _dbContext.Employees
            .Where(e => e.HomeDepartmentId != null && e.DepartmentId != e.HomeDepartmentId)
            .ToListAsync(cancellationToken);

        if (loaned.Count > 0)
        {
            var homeIds = loaned.Select(e => e.HomeDepartmentId!.Value).Distinct().ToList();
            var divisionByDept = await _dbContext.Departments
                .Where(d => homeIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Division })
                .ToDictionaryAsync(x => x.Id, x => x.Division, cancellationToken);

            foreach (var employee in loaned)
            {
                var home = employee.HomeDepartmentId!.Value;
                employee.DepartmentId = home;
                if (divisionByDept.TryGetValue(home, out var division))
                {
                    employee.Division = division;
                }
            }

            _auditWriter.Add(AuditAction.Update, "ShiftReset", null, null,
                new { Returned = loaned.Count, Trigger = trigger, Marker = marker });
        }

        settings.LastResetMarker = marker;
        settings.LastResetAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return loaned.Count;
    }

    /// <summary>Marker for the shift boundary that started the current shift window (e.g. "20260819-N").</summary>
    private string CurrentBoundaryMarker(TimeSpan dayStart, TimeSpan nightStart)
    {
        var now = _factoryClock.LocalNow;
        var time = now.TimeOfDay;
        var date = DateOnly.FromDateTime(now);

        if (time >= nightStart)
        {
            return $"{date:yyyyMMdd}-N";
        }
        if (time >= dayStart)
        {
            return $"{date:yyyyMMdd}-D";
        }
        // Before the day shift starts we are still inside the previous evening's night window.
        return $"{date.AddDays(-1):yyyyMMdd}-N";
    }

    private async Task<AllocationSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AllocationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new AllocationSettings { Id = AllocationSettings.SingletonId, UpdatedAtUtc = _clock.UtcNow };
            _dbContext.AllocationSettings.Add(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    private void Require(UserRole minimumRole)
    {
        if (!_currentUser.HasAtLeast(minimumRole))
        {
            throw new ForbiddenException();
        }
    }
}
