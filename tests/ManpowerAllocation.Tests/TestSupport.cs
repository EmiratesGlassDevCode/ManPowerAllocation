using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ManpowerAllocation.Tests;

/// <summary>Deterministic UTC clock for tests.</summary>
internal sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

/// <summary>Factory-local clock whose time can be set per test.</summary>
internal sealed class FakeFactoryClock : IFactoryClock
{
    public DateTime LocalNow { get; set; } = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);
}

/// <summary>Presence provider with settable current/previous sets and raw identifiers.</summary>
internal sealed class FakePresenceProvider : IPresenceProvider
{
    public bool IsConfigured { get; set; } = true;
    public ShiftPresence Presence { get; set; } = ShiftPresence.Empty;
    public IReadOnlyList<BiometricIdentity> Identities { get; set; } = Array.Empty<BiometricIdentity>();

    public Task<ShiftPresence> GetPresenceAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Presence);

    public Task<IReadOnlyList<BiometricIdentity>> GetRecentIdentitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Identities);
}

/// <summary>Builders and an InMemory context factory shared by the tests.</summary>
internal static class TestSupport
{
    public static ManpowerDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ManpowerDbContext>()
            .UseInMemoryDatabase($"mpa-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static ShiftPresence Presence(IEnumerable<string> current, IEnumerable<string>? previous = null) =>
        new(
            new HashSet<string>(current, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(previous ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase));

    public static Department Dept(
        int id = 1, Division division = Division.Egl, int requiredDay = 10, int requiredNight = 8, bool active = true) =>
        new()
        {
            Id = id,
            Division = division,
            Name = $"D{id}",
            RequiredDay = requiredDay,
            RequiredNight = requiredNight,
            IsActive = active,
            Sequence = id
        };

    public static Employee Emp(
        int id, string? badge, ShiftType shift, AttendanceStatus status,
        bool supply = false, int deptId = 1, Division division = Division.Egl) =>
        new()
        {
            Id = id,
            Name = $"E{id}",
            BadgeNumber = badge,
            Shift = shift,
            Status = status,
            IsSupply = supply,
            DepartmentId = deptId,
            Division = division
        };
}
