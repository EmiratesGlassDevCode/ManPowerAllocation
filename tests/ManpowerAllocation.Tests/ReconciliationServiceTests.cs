using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Reconciliation;
using ManpowerAllocation.Domain.Enums;
using Xunit;
using static ManpowerAllocation.Tests.TestSupport;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Tests the biometric reconciliation: matched vs unmatched identifiers, employees with no badge,
/// the internal status-count balance, and the safe result when no source is configured.
/// </summary>
public sealed class ReconciliationServiceTests
{
    private static async Task<ManpowerAllocation.Infrastructure.Persistence.ManpowerDbContext> Seed()
    {
        var db = NewContext();
        db.Departments.Add(Dept());
        db.Employees.AddRange(
            Emp(1, "100", ShiftType.Day, AttendanceStatus.Present),               // matches a punch
            Emp(2, "  ", ShiftType.Day, AttendanceStatus.Absent),                 // blank badge (own) -> no-badge list
            Emp(3, null, ShiftType.Day, AttendanceStatus.Present, supply: true),  // supply, no badge -> excluded
            Emp(4, "900", ShiftType.Night, AttendanceStatus.OnVacation));         // badged, never punched
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Splits_matched_and_unmatched_identifiers()
    {
        await using var db = await Seed();
        var presence = new FakePresenceProvider
        {
            Identities = new List<BiometricIdentity>
            {
                new("100", "Current Shift", new DateTime(2026, 1, 1, 9, 0, 0), 2), // matches employee 1
                new("777", "Current Shift", new DateTime(2026, 1, 1, 9, 5, 0), 1)  // no employee
            }
        };

        var report = await new ReconciliationService(db, presence, new FakeClock()).BuildAsync();

        Assert.True(report.SourceConfigured);
        Assert.Equal(2, report.BiometricIdCount);
        Assert.Equal(1, report.MatchedIdCount);
        Assert.Single(report.UnmatchedBiometricIds);
        Assert.Equal("777", report.UnmatchedBiometricIds[0].BiometricId);
    }

    [Fact]
    public async Task Lists_only_non_supply_employees_without_a_badge()
    {
        await using var db = await Seed();
        var report = await new ReconciliationService(db, new FakePresenceProvider(), new FakeClock()).BuildAsync();

        Assert.Single(report.EmployeesWithoutBadge);
        Assert.Equal(2, report.EmployeesWithoutBadge[0].EmployeeId); // the blank-badge own worker
        Assert.Equal(2, report.RosterBadgeCount);                    // "100" and "900" only
    }

    [Fact]
    public async Task Status_counts_balance_to_the_whole_roster()
    {
        await using var db = await Seed();
        var report = await new ReconciliationService(db, new FakePresenceProvider(), new FakeClock()).BuildAsync();

        Assert.True(report.StatusCountsBalance);
        Assert.Equal(report.RosterEmployeeCount,
            report.RosterPresent + report.RosterAbsent + report.RosterOnVacation);
    }

    [Fact]
    public async Task Unconfigured_source_reports_no_biometric_data_but_still_lists_no_badge()
    {
        await using var db = await Seed();
        var presence = new FakePresenceProvider { IsConfigured = false };

        var report = await new ReconciliationService(db, presence, new FakeClock()).BuildAsync();

        Assert.False(report.SourceConfigured);
        Assert.Equal(0, report.BiometricIdCount);
        Assert.Empty(report.UnmatchedBiometricIds);
        Assert.Single(report.EmployeesWithoutBadge);
    }
}
