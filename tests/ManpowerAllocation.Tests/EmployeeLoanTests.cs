using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Application.Employees;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Covers the loan (temporary move) rules: loans stay within a division unless a shared pool is
/// involved, in which case they may cross divisions.
/// </summary>
public sealed class EmployeeLoanTests
{
    private static async Task SeedAsync(Infrastructure.Persistence.ManpowerDbContext db)
    {
        db.Departments.Add(TestSupport.Dept(id: 1, division: Division.Egl));
        db.Departments.Add(TestSupport.Dept(id: 2, division: Division.Brg));
        db.Departments.Add(new Department
        {
            Id = 3, Division = Division.Brg, Name = "EXCESS", IsPool = true,
            RequiredDay = 0, RequiredNight = 0, IsActive = true, Sequence = 3
        });
        db.Employees.Add(TestSupport.Emp(101, "B101", ShiftType.Day, AttendanceStatus.Present, deptId: 1, division: Division.Egl));
        await db.SaveChangesAsync();
    }

    private static EmployeeService NewService(Infrastructure.Persistence.ManpowerDbContext db) =>
        new(db, new NullAuditWriter(), new FakeUser(UserRole.User));

    [Fact]
    public async Task Loan_across_divisions_without_a_pool_is_rejected()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = NewService(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.MoveAsync(101, new MoveEmployeeRequest { TargetDepartmentId = 2 }));
    }

    [Fact]
    public async Task Loan_across_divisions_through_a_pool_is_allowed()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.MoveAsync(101, new MoveEmployeeRequest { TargetDepartmentId = 3 });

        Assert.Equal(3, result.DepartmentId);
        Assert.Equal(Division.Brg, result.Division);
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

    private sealed class NullAuditWriter : IAuditWriter
    {
        public void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue) { }
    }
}
