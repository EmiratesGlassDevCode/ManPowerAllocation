using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Application.Departments;
using ManpowerAllocation.Application.Employees;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Verifies that a Department Head may act only within their assigned departments, while a plain
/// User/Admin is unaffected. Enforcement lives in the services (server-side).
/// </summary>
public sealed class DepartmentHeadScopeTests
{
    private static async Task SeedAsync(Infrastructure.Persistence.ManpowerDbContext db)
    {
        db.Departments.Add(TestSupport.Dept(id: 1, division: Division.Egl));
        db.Departments.Add(TestSupport.Dept(id: 2, division: Division.Egl));
        db.Employees.Add(TestSupport.Emp(101, "B101", ShiftType.Day, AttendanceStatus.Present, deptId: 1));
        db.Employees.Add(TestSupport.Emp(202, "B202", ShiftType.Day, AttendanceStatus.Present, deptId: 2));
        db.DepartmentManagers.Add(new DepartmentManager { EntraObjectId = "head-1", DepartmentId = 1, CreatedAtUtc = new DateTime(2026, 1, 1) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Head_can_change_status_of_employee_in_managed_department()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = new EmployeeService(db, new NullAuditWriter(), Head("head-1"));

        var result = await svc.ChangeStatusAsync(101, new ChangeStatusRequest { Status = AttendanceStatus.OnVacation });

        Assert.Equal(AttendanceStatus.OnVacation, result.Status);
    }

    [Fact]
    public async Task Head_cannot_change_status_of_employee_outside_scope()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = new EmployeeService(db, new NullAuditWriter(), Head("head-1"));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.ChangeStatusAsync(202, new ChangeStatusRequest { Status = AttendanceStatus.Absent }));
    }

    [Fact]
    public async Task Head_can_edit_requirements_of_managed_department_but_not_others()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = new DepartmentService(db, new NullAuditWriter(), Head("head-1"));

        var updated = await svc.UpdateAsync(1, new UpdateDepartmentRequest { RequiredDay = 12, RequiredNight = 8, Sequence = 1, IsActive = true });
        Assert.Equal(12, updated.RequiredDay);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.UpdateAsync(2, new UpdateDepartmentRequest { RequiredDay = 5, RequiredNight = 5, Sequence = 1, IsActive = true }));
    }

    [Fact]
    public async Task Plain_user_can_edit_any_department_status_but_not_requirements()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var empSvc = new EmployeeService(db, new NullAuditWriter(), Simple(UserRole.User));
        var deptSvc = new DepartmentService(db, new NullAuditWriter(), Simple(UserRole.User));

        // A User may change status in any department...
        await empSvc.ChangeStatusAsync(202, new ChangeStatusRequest { Status = AttendanceStatus.Absent });
        // ...but may not edit department requirements (Admin/head only).
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            deptSvc.UpdateAsync(1, new UpdateDepartmentRequest { RequiredDay = 1, RequiredNight = 1, Sequence = 1, IsActive = true }));
    }

    private static ICurrentUser Head(string id) => new FakeUser(UserRole.DepartmentHead, id);
    private static ICurrentUser Simple(UserRole role) => new FakeUser(role, "u");

    private sealed class FakeUser : ICurrentUser
    {
        public FakeUser(UserRole role, string id) { Role = role; UserId = id; }
        public string UserId { get; }
        public string? DisplayName => "test";
        public bool IsAuthenticated => true;
        public bool IsBreakGlassSession => false;
        public UserRole Role { get; }

        public bool HasAtLeast(UserRole minimumRole)
        {
            // DepartmentHead ranks only as Viewer on the global ladder.
            static int Rank(UserRole r) => r == UserRole.DepartmentHead ? (int)UserRole.Viewer : (int)r;
            return Rank(Role) >= Rank(minimumRole);
        }
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue) { }
    }
}
