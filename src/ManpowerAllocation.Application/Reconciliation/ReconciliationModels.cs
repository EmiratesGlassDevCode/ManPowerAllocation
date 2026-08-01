using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Reconciliation;

/// <summary>
/// A point-in-time comparison of the biometric attendance source against the governed roster. It
/// answers two operational questions on one screen: "did we really pull data, and do the numbers
/// add up?" (the verification counts) and "who is unaccounted for?" (the two reconciliation lists).
/// </summary>
/// <param name="SourceConfigured">False when no attendance source is configured; the biometric side is then empty.</param>
/// <param name="GeneratedAtUtc">When this report was built.</param>
/// <param name="BiometricIdCount">Distinct identifiers the view currently exposes.</param>
/// <param name="BiometricPunchCount">Total rows/punches behind those identifiers.</param>
/// <param name="CurrentShiftCheckedIn">Distinct identifiers checked in on the live shift.</param>
/// <param name="PreviousShiftCheckedIn">Distinct identifiers checked in on the shift that ran before.</param>
/// <param name="MostRecentPunch">The latest check-in time across the view, as stored by the source.</param>
/// <param name="MatchedIdCount">Pulled identifiers that matched a roster badge.</param>
/// <param name="RosterEmployeeCount">Total employees on the roster.</param>
/// <param name="RosterSupplyCount">Outsource/supply employees (not expected in the biometric feed).</param>
/// <param name="RosterBadgeCount">Distinct non-blank badges across the roster.</param>
/// <param name="RosterPresent">Employees whose stored status is Present.</param>
/// <param name="RosterAbsent">Employees whose stored status is Absent.</param>
/// <param name="RosterOnVacation">Employees whose stored status is On Vacation.</param>
/// <param name="UnmatchedBiometricIds">Punches whose identifier matches no employee badge.</param>
/// <param name="EmployeesWithoutBadge">Non-supply employees with no badge — they can never be matched.</param>
public sealed record ReconciliationReport(
    bool SourceConfigured,
    DateTime GeneratedAtUtc,
    int BiometricIdCount,
    int BiometricPunchCount,
    int CurrentShiftCheckedIn,
    int PreviousShiftCheckedIn,
    DateTime? MostRecentPunch,
    int MatchedIdCount,
    int RosterEmployeeCount,
    int RosterSupplyCount,
    int RosterBadgeCount,
    int RosterPresent,
    int RosterAbsent,
    int RosterOnVacation,
    IReadOnlyList<UnmatchedBiometricId> UnmatchedBiometricIds,
    IReadOnlyList<EmployeeWithoutBadge> EmployeesWithoutBadge)
{
    /// <summary>Identifiers pulled that did not match any roster badge.</summary>
    public int UnmatchedIdCount => UnmatchedBiometricIds.Count;

    /// <summary>Non-supply employees carrying no badge.</summary>
    public int EmployeesWithoutBadgeCount => EmployeesWithoutBadge.Count;

    /// <summary>Non-supply employees, i.e. those expected to appear in the biometric feed.</summary>
    public int RosterOwnCount => RosterEmployeeCount - RosterSupplyCount;

    /// <summary>
    /// True when the stored status buckets sum to the whole roster — an exact internal-consistency
    /// check that the attendance calculation left no employee uncounted.
    /// </summary>
    public bool StatusCountsBalance =>
        RosterPresent + RosterAbsent + RosterOnVacation == RosterEmployeeCount;
}

/// <summary>A biometric punch whose identifier matches no employee badge on the roster.</summary>
/// <param name="BiometricId">The identifier recorded by the attendance system.</param>
/// <param name="ShiftLabel">The shift label on its most recent punch, if any.</param>
/// <param name="LastSeen">The most recent check-in time, as stored by the source.</param>
/// <param name="PunchCount">How many rows carried this identifier.</param>
public sealed record UnmatchedBiometricId(string BiometricId, string? ShiftLabel, DateTime? LastSeen, int PunchCount);

/// <summary>A non-supply employee with no badge, so the biometric system can never match them.</summary>
/// <param name="EmployeeId">The employee's surrogate id.</param>
/// <param name="Name">Employee name.</param>
/// <param name="Division">The division they sit in.</param>
/// <param name="DepartmentName">Their department.</param>
/// <param name="Shift">Their assigned shift.</param>
public sealed record EmployeeWithoutBadge(int EmployeeId, string Name, Division Division, string DepartmentName, ShiftType Shift);
