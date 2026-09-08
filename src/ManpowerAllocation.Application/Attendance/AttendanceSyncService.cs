using System.Text.Json;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    // Fallback schedule used only when a department has no schedule row (should not happen once the
    // schedules are seeded): 07:00 day start with a one-hour grace.
    private static readonly TimeSpan DefaultDayStart = new(7, 0, 0);
    private const int DefaultGraceMinutes = 60;

    private readonly IApplicationDbContext _dbContext;
    private readonly IPresenceProvider _presenceProvider;
    private readonly IClock _clock;
    private readonly IFactoryClock _factoryClock;
    private readonly AttendanceSyncStatus _status;
    private readonly ILogger<AttendanceSyncService> _logger;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The governed application persistence context.</param>
    /// <param name="presenceProvider">Provider of raw biometric check-in punches.</param>
    /// <param name="clock">Clock used for the run timestamp.</param>
    /// <param name="factoryClock">Local (factory) clock used to resolve each employee's shift window.</param>
    /// <param name="status">Shared holder for the most recent run result.</param>
    /// <param name="logger">Logger used to record the real cause of a failed run.</param>
    public AttendanceSyncService(
        IApplicationDbContext dbContext,
        IPresenceProvider presenceProvider,
        IClock clock,
        IFactoryClock factoryClock,
        AttendanceSyncStatus status,
        ILogger<AttendanceSyncService> logger)
    {
        _dbContext = dbContext;
        _presenceProvider = presenceProvider;
        _clock = clock;
        _factoryClock = factoryClock;
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
            // Raw check-in punches, grouped by badge. Presence is decided per employee against
            // their own department schedule, so different departments (e.g. 06:00–18:00 vs
            // 07:00–19:00) are each evaluated against their own windows.
            var punches = await _presenceProvider.GetRecentPunchesAsync(cancellationToken);
            var punchesByBadge = punches
                .Where(p => !string.IsNullOrWhiteSpace(p.BadgeNumber))
                .GroupBy(p => p.BadgeNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(p => p.InTime).ToList(), StringComparer.OrdinalIgnoreCase);

            var localNow = _factoryClock.LocalNow;

            // Load the schedules and departments once so each employee can be mapped to its window.
            var scheduleById = (await _dbContext.ShiftSchedules.AsNoTracking().ToListAsync(cancellationToken))
                .ToDictionary(s => s.Id);
            var departmentById = (await _dbContext.Departments.AsNoTracking().ToListAsync(cancellationToken))
                .ToDictionary(d => d.Id);

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

                // Resolve this employee's schedule (day start + grace) from their department.
                var dayStart = DefaultDayStart;
                var grace = DefaultGraceMinutes;
                if (departmentById.TryGetValue(employee.DepartmentId, out var department)
                    && scheduleById.TryGetValue(department.ShiftScheduleId, out var schedule))
                {
                    dayStart = schedule.DayStart;
                    grace = schedule.GraceMinutes;
                }

                // Present when any of this badge's check-ins falls inside their shift's window (the
                // live occurrence, or the most recent past one — keeping the board cumulative).
                var times = punchesByBadge.TryGetValue(badge, out var list) ? list : Enumerable.Empty<DateTime>();
                var checkedIn = ShiftWindowResolver.IsPresent(times, dayStart, grace, employee.Shift, localNow);

                var target = checkedIn
                    ? AttendanceStatus.Present
                    : employee.Status == AttendanceStatus.OnVacation
                        ? AttendanceStatus.OnVacation
                        : AttendanceStatus.Absent;

                if (target != employee.Status)
                {
                    employee.Status = target;
                    changed++;
                }

                Tally(target, ref present, ref absent, ref onVacation);
            }

            // Record every successful run in the audit trail — one summary entry per run — so there is
            // a persistent history of the scheduled (default 10-minute) syncs, not only the runs that
            // changed a status. Employee status changes (when any) are saved in the same transaction.
            WriteSummaryAudit(triggeredBy, present, absent, onVacation, changed);
            await SaveWithConcurrencyRetryAsync(cancellationToken);

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

    /// <summary>
    /// Saves the sync's status changes, tolerating a concurrent user edit. If someone changed an
    /// employee mid-sync, that one row's conflict would otherwise abort the whole batch (and be
    /// mis-reported as an attendance-source read failure). Instead we reload the conflicting rows —
    /// keeping the user's edit — and save the rest.
    /// </summary>
    private async Task SaveWithConcurrencyRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                await entry.ReloadAsync(cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
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
