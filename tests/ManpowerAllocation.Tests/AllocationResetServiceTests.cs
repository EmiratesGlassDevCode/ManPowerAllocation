using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Allocation;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Application.Settings;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>Covers the shift-loan reset: the enable gate and returning loaned staff to their home.</summary>
public sealed class AllocationResetServiceTests
{
    private static async Task SeedAsync(Infrastructure.Persistence.ManpowerDbContext db, bool masterUploaded)
    {
        db.Departments.Add(TestSupport.Dept(id: 1, division: Division.Egl));
        db.Departments.Add(TestSupport.Dept(id: 2, division: Division.Brg));
        // Employee 101 is home in dept 1 but currently loaned to dept 2 (different division).
        var e = TestSupport.Emp(101, "B101", ShiftType.Day, AttendanceStatus.Present, deptId: 2, division: Division.Brg);
        e.HomeDepartmentId = 1;
        db.Employees.Add(e);
        db.AllocationSettings.Add(new AllocationSettings
        {
            Id = AllocationSettings.SingletonId,
            AutoShiftResetEnabled = false,
            LastMasterUploadUtc = masterUploaded ? new DateTime(2026, 1, 1) : null,
            UpdatedAtUtc = new DateTime(2026, 1, 1)
        });
        await db.SaveChangesAsync();
    }

    private static AllocationResetService NewService(Infrastructure.Persistence.ManpowerDbContext db, UserRole role) =>
        new(db, new NullAudit(), new FakeClock(),
            new FakeFactoryClock { LocalNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified) },
            new FakeShiftSettings(), new FakeUser(role));

    [Fact]
    public async Task Reset_now_returns_loaned_employee_to_home()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db, masterUploaded: true);

        var count = await NewService(db, UserRole.User).ResetNowAsync();

        Assert.Equal(1, count);
        var emp = db.Employees.Single(x => x.Id == 101);
        Assert.Equal(1, emp.DepartmentId);         // back to home
        Assert.Equal(Division.Egl, emp.Division);  // division re-aligned to home
    }

    [Fact]
    public async Task Enable_is_blocked_until_a_master_sheet_is_uploaded()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db, masterUploaded: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => NewService(db, UserRole.Admin).SetEnabledAsync(true));
    }

    [Fact]
    public async Task Enable_succeeds_after_a_master_upload()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db, masterUploaded: true);

        await NewService(db, UserRole.Admin).SetEnabledAsync(true);

        Assert.True(db.AllocationSettings.Single().AutoShiftResetEnabled);
    }

    private sealed class FakeShiftSettings : IShiftSettingsService
    {
        public Task<ShiftSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShiftSettingsDto(new TimeSpan(7, 0, 0), new TimeSpan(19, 0, 0), new DateTime(2026, 1, 1), null));

        public Task<ShiftSettingsDto> UpdateAsync(UpdateShiftSettingsRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeUser : ICurrentUser
    {
        public FakeUser(UserRole role) => Role = role;
        public string UserId => "u";
        public string? DisplayName => "test";
        public bool IsAuthenticated => true;
        public bool IsBreakGlassSession => false;
        public UserRole Role { get; }
        public bool HasAtLeast(UserRole minimumRole) => (int)Role >= (int)minimumRole;
    }

    private sealed class NullAudit : IAuditWriter
    {
        public void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue) { }
    }
}
