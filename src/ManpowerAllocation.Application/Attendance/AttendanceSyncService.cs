using System.Text.Json;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Application.Attendance;

/// <summary>
/// Default <see cref="IAttendanceSyncService"/>. Applies the presence rule agreed with the
/// business and records each run as one summary audit entry (actor "attendance-sync") rather
/// than one entry per employee, keeping the audit trail meaningful without flooding it — the
/// authoritative per-person history already lives in the attendance system.
/// </summary>
public sealed class AttendanceSyncService : IAttendanceSyncService
{
    private const string SyncActor = "attendance-sync";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IPresenceProvider _presenceProvider;
    private readonly IClock _clock;
    private readonly IFactoryClock _factoryClock;
    private readonly ShiftWindowOptions _shiftWindow;
    private readonly AttendanceSyncStatus _status;
    private readonly ILogger<AttendanceSyncService> _logger;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The governed application persistence context.</param>
    /// <param name="presenceProvider">Provider of today's present employee identifiers.</param>
    /// <param name="clock">Clock used for the run timestamp.</param>
    /// <param name="factoryClock">Local (factory) clock used to decide which shift is currently live.</param>
    /// <param name="shiftWindow">Grace-window tuning for scoping presence to a shift.</param>
    /// <param name="status">Shared holder for the most recent run result.</param>
    /// <param name="logger">Logger used to record the real cause of a failed run.</param>
    public AttendanceSyncService(
        IApplicationDbContext dbContext,
        IPresenceProvider presenceProvider,
        IClock clock,
        IFactoryClock factoryClock,
        IOptions<ShiftWindowOptions> shiftWindow,
        AttendanceSyncStatus status,
        ILogger<AttendanceSyncService> logger)
    {
        _dbContext = dbContext;
        _presenceProvider = presenceProvider;
        _clock = clock;
        _factoryClock = factoryClock;
        _shiftWindow = shiftWindow.Value;
        _status = status;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AttendanceSyncResult> SyncAsync(string triggeredBy, CancellationToken cancellationToken = default)
    {
        // Guard: with no configured attendance source, do nothing rather than mark everyone absent.
        if (!_presenceProvider.IsConfigured)
        {
            return Record(new AttendanceSyncResult
            {
                RanAtUtc = _clock.UtcNow,
                Success = false,
                Error = "No attendance source is configured."
            });
        }

        try
        {
            var presentIds = await _presenceProvider.GetPresentEmployeeIdsForTodayAsync(cancellationToken);

            // The biometric feed only returns punches for the shift that is currently live, so an
            // employee may only be auto-marked present/absent while their OWN shift is live. This
            // stops a night worker being shown absent in the morning (and a day worker at night):
            // off-shift employees keep their last status until their shift window opens.
            var liveShift = await ResolveLiveShiftAsync(cancellationToken);

            // Outsource/supply workers are not in the biometric system, so their status is left alone.
            var employees = await _dbContext.Employees
                .Where(e => !e.IsSupply)
                .ToListAsync(cancellationToken);

            int present = 0, absent = 0, onVacation = 0, changed = 0;

            foreach (var employee in employees)
            {
                var badge = employee.BadgeNumber?.Trim();

                // A blank badge means the employee cannot be matched against the biometric
                // system yet — it does NOT mean they are absent. Leave their status untouched
                // (as with supply workers) so a manually-set status is not overwritten every
                // sync tick until a badge is assigned.
                if (string.IsNullOrEmpty(badge))
                {
                    Tally(employee.Status, ref present, ref absent, ref onVacation);
                    continue;
                }

                var checkedIn = presentIds.Contains(badge);

                AttendanceStatus target;
                if (checkedIn)
                {
                    // A punch always wins: they are physically here now, whatever shift they are
                    // assigned to. This covers someone rostered to one shift who works the other.
                    target = AttendanceStatus.Present;
                }
                else if (employee.Shift == liveShift)
                {
                    // Their own shift is live but there is no punch — absent (a supervisor's
                    // vacation is preserved).
                    target = employee.Status == AttendanceStatus.OnVacation
                        ? AttendanceStatus.OnVacation
                        : AttendanceStatus.Absent;
                }
                else
                {
                    // Off-shift and not punched in: keep the last status so a night worker is not
                    // shown absent during the morning (and a day worker at night).
                    target = employee.Status;
                }

                if (target != employee.Status)
                {
                    employee.Status = target;
                    changed++;
                }

                Tally(target, ref present, ref absent, ref onVacation);
            }

            if (changed > 0)
            {
                WriteSummaryAudit(triggeredBy, present, absent, onVacation, changed);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Record(new AttendanceSyncResult
            {
                RanAtUtc = _clock.UtcNow,
                Success = true,
                Present = present,
                Absent = absent,
                OnVacation = onVacation,
                Changed = changed
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never throw out of the sync: the dashboards must keep working off the last-known
            // state even when the attendance database is briefly unreachable. The user-facing
            // message stays generic, but the true cause (missing view, denied SELECT, column or
            // type mismatch, unreachable server) is logged here so operators can diagnose it.
            _logger.LogError(
                ex,
                "Attendance sync failed while reading the external source (triggered by {TriggeredBy}). {ExceptionType}: {ExceptionMessage}",
                triggeredBy,
                ex.GetType().Name,
                ex.Message);

            return Record(new AttendanceSyncResult
            {
                RanAtUtc = _clock.UtcNow,
                Success = false,
                Error = "The attendance source could not be read."
            });
        }
    }

    /// <summary>Increments the matching status counter.</summary>
    private static void Tally(AttendanceStatus status, ref int present, ref int absent, ref int onVacation)
    {
        switch (status)
        {
            case AttendanceStatus.Present: present++; break;
            case AttendanceStatus.OnVacation: onVacation++; break;
            default: absent++; break;
        }
    }

    /// <summary>
    /// Determines which shift is live in factory-local time, opening each shift's window
    /// <see cref="ShiftWindowOptions.GraceMinutes"/> before its official start so early comers count.
    /// </summary>
    private async Task<ShiftType> ResolveLiveShiftAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.ShiftSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ShiftSetting.SingletonId, cancellationToken);

        var dayStart = settings?.DayShiftStart ?? new TimeSpan(7, 0, 0);
        var nightStart = settings?.NightShiftStart ?? new TimeSpan(19, 0, 0);
        var grace = TimeSpan.FromMinutes(Math.Max(0, _shiftWindow.GraceMinutes));

        var now = _factoryClock.LocalNow.TimeOfDay;
        var dayFrom = WrapToDay(dayStart - grace);
        var nightFrom = WrapToDay(nightStart - grace);

        // Day is live from dayFrom until nightFrom; the rest of the 24h cycle is night.
        bool dayLive = dayFrom <= nightFrom
            ? now >= dayFrom && now < nightFrom
            : now >= dayFrom || now < nightFrom;

        return dayLive ? ShiftType.Day : ShiftType.Night;
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

    /// <summary>Writes a single summary audit entry describing the run.</summary>
    private void WriteSummaryAudit(string triggeredBy, int present, int absent, int onVacation, int changed)
    {
        var summary = new { Trigger = triggeredBy, Present = present, Absent = absent, OnVacation = onVacation, Changed = changed };
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = SyncActor,
            UserDisplayName = "Attendance synchronisation",
            TimestampUtc = _clock.UtcNow,
            Action = AuditAction.Update,
            EntityName = "AttendanceSync",
            RecordId = null,
            OldValue = null,
            NewValue = JsonSerializer.Serialize(summary, SerializerOptions),
            IsBreakGlassSession = false
        });
    }

    /// <summary>Stores the result for the admin screen and returns it.</summary>
    private AttendanceSyncResult Record(AttendanceSyncResult result)
    {
        _status.LastRun = result;
        return result;
    }
}
