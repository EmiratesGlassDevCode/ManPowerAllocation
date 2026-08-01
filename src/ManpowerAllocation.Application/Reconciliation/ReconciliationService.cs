using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Reconciliation;

/// <summary>
/// Default <see cref="IReconciliationService"/>. Reads the roster from the governed database and the
/// raw identifiers from the biometric source, then matches them badge-for-badge (trimmed,
/// case-insensitive — the same comparison the sync uses) to produce the verification counts and the
/// two unaccounted-for lists.
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPresenceProvider _presenceProvider;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The governed application persistence context.</param>
    /// <param name="presenceProvider">Provider over the external biometric attendance source.</param>
    /// <param name="clock">Clock used to stamp the report.</param>
    public ReconciliationService(
        IApplicationDbContext dbContext,
        IPresenceProvider presenceProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _presenceProvider = presenceProvider;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ReconciliationReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = _clock.UtcNow;

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .ToListAsync(cancellationToken);

        // Match punches against EVERY non-blank badge (supply included), so a badged outsource
        // worker who punches is not mis-reported as an unmatched identifier. The comparison mirrors
        // the sync: trimmed and case-insensitive.
        var rosterBadges = new HashSet<string>(
            employees
                .Select(e => e.BadgeNumber?.Trim())
                .Where(b => !string.IsNullOrEmpty(b))
                .Select(b => b!),
            StringComparer.OrdinalIgnoreCase);

        // Only non-supply employees are expected to carry a badge; outsource workers are
        // legitimately absent from the biometric system, so they are never flagged here.
        var withoutBadge = employees
            .Where(e => !e.IsSupply && string.IsNullOrWhiteSpace(e.BadgeNumber))
            .OrderBy(e => e.Division)
            .ThenBy(e => e.Department is null ? string.Empty : e.Department.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new EmployeeWithoutBadge(e.Id, e.Name, e.Division, e.Department?.Name ?? "—", e.Shift))
            .ToList();

        var rosterSupply = employees.Count(e => e.IsSupply);
        var rosterPresent = employees.Count(e => e.Status == AttendanceStatus.Present);
        var rosterAbsent = employees.Count(e => e.Status == AttendanceStatus.Absent);
        var rosterOnVacation = employees.Count(e => e.Status == AttendanceStatus.OnVacation);

        if (!_presenceProvider.IsConfigured)
        {
            return new ReconciliationReport(
                SourceConfigured: false,
                GeneratedAtUtc: generatedAt,
                BiometricIdCount: 0,
                BiometricPunchCount: 0,
                CurrentShiftCheckedIn: 0,
                PreviousShiftCheckedIn: 0,
                MostRecentPunch: null,
                MatchedIdCount: 0,
                RosterEmployeeCount: employees.Count,
                RosterSupplyCount: rosterSupply,
                RosterBadgeCount: rosterBadges.Count,
                RosterPresent: rosterPresent,
                RosterAbsent: rosterAbsent,
                RosterOnVacation: rosterOnVacation,
                UnmatchedBiometricIds: Array.Empty<UnmatchedBiometricId>(),
                EmployeesWithoutBadge: withoutBadge);
        }

        // Two reads of the same small, rolling view: the shift-bucketed present sets (for the
        // "checked-in" counts) and the full distinct set of identifiers (for the pull proof and the
        // unmatched list).
        var presence = await _presenceProvider.GetPresenceAsync(cancellationToken);
        var identities = await _presenceProvider.GetRecentIdentitiesAsync(cancellationToken);

        var unmatched = identities
            .Where(i => !rosterBadges.Contains(i.BadgeNumber))
            .Select(i => new UnmatchedBiometricId(i.BadgeNumber, i.ShiftLabel, i.LastSeen, i.PunchCount))
            .ToList();

        var matchedIdCount = identities.Count - unmatched.Count;
        var mostRecentPunch = identities.Count == 0
            ? (DateTime?)null
            : identities.Max(i => i.LastSeen);

        return new ReconciliationReport(
            SourceConfigured: true,
            GeneratedAtUtc: generatedAt,
            BiometricIdCount: identities.Count,
            BiometricPunchCount: identities.Sum(i => i.PunchCount),
            CurrentShiftCheckedIn: presence.CurrentShift.Count,
            PreviousShiftCheckedIn: presence.PreviousShift.Count,
            MostRecentPunch: mostRecentPunch,
            MatchedIdCount: matchedIdCount,
            RosterEmployeeCount: employees.Count,
            RosterSupplyCount: rosterSupply,
            RosterBadgeCount: rosterBadges.Count,
            RosterPresent: rosterPresent,
            RosterAbsent: rosterAbsent,
            RosterOnVacation: rosterOnVacation,
            UnmatchedBiometricIds: unmatched,
            EmployeesWithoutBadge: withoutBadge);
    }
}
