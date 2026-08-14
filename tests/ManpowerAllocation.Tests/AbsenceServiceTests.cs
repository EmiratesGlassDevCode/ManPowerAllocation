using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Absences;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>
/// Covers the Absentees feature: the list content, department-scoped editing (heads vs users),
/// Informed date-range validation and expiry drop-off, Not-Informed comments, clearing, and the
/// admin-only category management.
/// </summary>
public sealed class AbsenceServiceTests
{
    private static readonly DateTime Today = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private static async Task<(int informedCatId, int notInformedCatId)> SeedAsync(Infrastructure.Persistence.ManpowerDbContext db)
    {
        db.Departments.Add(TestSupport.Dept(id: 1, division: Division.Egl));
        db.Departments.Add(TestSupport.Dept(id: 2, division: Division.Egl));
        db.Employees.Add(TestSupport.Emp(101, "B101", ShiftType.Day, AttendanceStatus.Absent, deptId: 1));
        db.Employees.Add(TestSupport.Emp(202, "B202", ShiftType.Day, AttendanceStatus.Absent, deptId: 2));
        db.DepartmentManagers.Add(new DepartmentManager { EntraObjectId = "head-1", DepartmentId = 1, CreatedAtUtc = new DateTime(2026, 1, 1) });

        var informed = new AbsenceReasonCategory { Kind = AbsenceKind.Informed, Name = "Annual Leave", Sequence = 1, IsActive = true, CreatedAtUtc = new DateTime(2026, 1, 1) };
        var notInformed = new AbsenceReasonCategory { Kind = AbsenceKind.NotInformed, Name = "No Show", Sequence = 1, IsActive = true, CreatedAtUtc = new DateTime(2026, 1, 1) };
        db.AbsenceReasonCategories.Add(informed);
        db.AbsenceReasonCategories.Add(notInformed);

        await db.SaveChangesAsync();
        return (informed.Id, notInformed.Id);
    }

    private static AbsenceService NewService(Infrastructure.Persistence.ManpowerDbContext db, ICurrentUser user) =>
        new(db, new NullAuditWriter(), new FakeFactoryClock { LocalNow = Today }, new FakeClock(), user);

    [Fact]
    public async Task Absentees_list_includes_absent_employees_across_departments()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = NewService(db, Simple(UserRole.User));

        var list = await svc.GetAbsenteesAsync(null);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, i => i.EmployeeId == 101);
        Assert.Contains(list, i => i.EmployeeId == 202);
        Assert.All(list, i => Assert.Null(i.Reason)); // no reasons recorded yet
    }

    [Fact]
    public async Task Head_can_set_informed_reason_in_own_department()
    {
        using var db = TestSupport.NewContext();
        var (informed, _) = await SeedAsync(db);
        var svc = NewService(db, Head("head-1"));

        await svc.SetReasonAsync(new SetAbsenceReasonRequest
        {
            EmployeeId = 101,
            CategoryId = informed,
            FromDate = new DateOnly(2026, 1, 14),
            ToDate = new DateOnly(2026, 1, 20),
            Comment = "Planned leave"
        });

        var row = (await svc.GetAbsenteesAsync(null)).Single(i => i.EmployeeId == 101);
        Assert.NotNull(row.Reason);
        Assert.Equal(AbsenceKind.Informed, row.Reason!.Kind);
        Assert.Equal(new DateOnly(2026, 1, 20), row.Reason.ToDate);
    }

    [Fact]
    public async Task Head_cannot_set_reason_outside_scope()
    {
        using var db = TestSupport.NewContext();
        var (informed, _) = await SeedAsync(db);
        var svc = NewService(db, Head("head-1"));

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.SetReasonAsync(new SetAbsenceReasonRequest
        {
            EmployeeId = 202, // department 2 — not managed by head-1
            CategoryId = informed,
            FromDate = new DateOnly(2026, 1, 14),
            ToDate = new DateOnly(2026, 1, 20)
        }));
    }

    [Fact]
    public async Task Informed_requires_a_date_range()
    {
        using var db = TestSupport.NewContext();
        var (informed, _) = await SeedAsync(db);
        var svc = NewService(db, Simple(UserRole.User));

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.SetReasonAsync(new SetAbsenceReasonRequest
        {
            EmployeeId = 101,
            CategoryId = informed,
            FromDate = null,
            ToDate = null
        }));
    }

    [Fact]
    public async Task Informed_rejects_end_before_start()
    {
        using var db = TestSupport.NewContext();
        var (informed, _) = await SeedAsync(db);
        var svc = NewService(db, Simple(UserRole.User));

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.SetReasonAsync(new SetAbsenceReasonRequest
        {
            EmployeeId = 101,
            CategoryId = informed,
            FromDate = new DateOnly(2026, 1, 20),
            ToDate = new DateOnly(2026, 1, 10)
        }));
    }

    [Fact]
    public async Task NotInformed_stores_comment_without_dates()
    {
        using var db = TestSupport.NewContext();
        var (_, notInformed) = await SeedAsync(db);
        var svc = NewService(db, Simple(UserRole.User));

        await svc.SetReasonAsync(new SetAbsenceReasonRequest
        {
            EmployeeId = 101,
            CategoryId = notInformed,
            Comment = "Did not call in"
        });

        var row = (await svc.GetAbsenteesAsync(null)).Single(i => i.EmployeeId == 101);
        Assert.Equal(AbsenceKind.NotInformed, row.Reason!.Kind);
        Assert.Null(row.Reason.ToDate);
        Assert.Equal("Did not call in", row.Reason.Comment);
    }

    [Fact]
    public async Task Expired_informed_absence_drops_off_the_list()
    {
        using var db = TestSupport.NewContext();
        var (informed, _) = await SeedAsync(db);

        // A present employee whose Informed leave ended before today: must NOT appear.
        db.Employees.Add(TestSupport.Emp(303, "B303", ShiftType.Day, AttendanceStatus.Present, deptId: 1));
        // A present employee currently on Informed leave (covers today): SHOULD appear.
        db.Employees.Add(TestSupport.Emp(304, "B304", ShiftType.Day, AttendanceStatus.Present, deptId: 1));
        db.EmployeeAbsences.Add(new EmployeeAbsence
        {
            EmployeeId = 303, CategoryId = informed, Kind = AbsenceKind.Informed,
            FromDate = new DateOnly(2026, 1, 1), ToDate = new DateOnly(2026, 1, 10),
            CreatedByObjectId = "u", CreatedAtUtc = new DateTime(2026, 1, 1)
        });
        db.EmployeeAbsences.Add(new EmployeeAbsence
        {
            EmployeeId = 304, CategoryId = informed, Kind = AbsenceKind.Informed,
            FromDate = new DateOnly(2026, 1, 12), ToDate = new DateOnly(2026, 1, 20),
            CreatedByObjectId = "u", CreatedAtUtc = new DateTime(2026, 1, 12)
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, Simple(UserRole.User));
        var list = await svc.GetAbsenteesAsync(null);

        Assert.DoesNotContain(list, i => i.EmployeeId == 303); // expired → gone
        Assert.Contains(list, i => i.EmployeeId == 304);        // active informed leave → shown
    }

    [Fact]
    public async Task Clear_removes_the_current_reason()
    {
        using var db = TestSupport.NewContext();
        var (_, notInformed) = await SeedAsync(db);
        var svc = NewService(db, Simple(UserRole.User));

        await svc.SetReasonAsync(new SetAbsenceReasonRequest { EmployeeId = 101, CategoryId = notInformed, Comment = "x" });
        await svc.ClearReasonAsync(101);

        var row = (await svc.GetAbsenteesAsync(null)).Single(i => i.EmployeeId == 101);
        Assert.Null(row.Reason);
    }

    [Fact]
    public async Task Category_creation_requires_admin()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = new AbsenceCategoryService(db, new NullAuditWriter(), new FakeClock(), Simple(UserRole.User));

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.CreateAsync(new CreateAbsenceCategoryRequest
        {
            Kind = AbsenceKind.Informed,
            Name = "Study Leave"
        }));
    }

    [Fact]
    public async Task Admin_can_create_category()
    {
        using var db = TestSupport.NewContext();
        await SeedAsync(db);
        var svc = new AbsenceCategoryService(db, new NullAuditWriter(), new FakeClock(), Simple(UserRole.Admin));

        var created = await svc.CreateAsync(new CreateAbsenceCategoryRequest { Kind = AbsenceKind.Informed, Name = "Study Leave" });

        Assert.True(created.Id > 0);
        Assert.Equal(AbsenceKind.Informed, created.Kind);
        Assert.True(created.IsActive);
    }

    private static ICurrentUser Head(string id) => new FakeAbsenceUser(UserRole.DepartmentHead, id);
    private static ICurrentUser Simple(UserRole role) => new FakeAbsenceUser(role, "u");

    private sealed class FakeAbsenceUser : ICurrentUser
    {
        public FakeAbsenceUser(UserRole role, string id) { Role = role; UserId = id; }
        public string UserId { get; }
        public string? DisplayName => "test";
        public bool IsAuthenticated => true;
        public bool IsBreakGlassSession => false;
        public UserRole Role { get; }

        public bool HasAtLeast(UserRole minimumRole)
        {
            static int Rank(UserRole r) => r == UserRole.DepartmentHead ? (int)UserRole.Viewer : (int)r;
            return Rank(Role) >= Rank(minimumRole);
        }
    }

    private sealed class NullAuditWriter : IAuditWriter
    {
        public void Add(AuditAction action, string entityName, string? recordId, object? oldValue, object? newValue) { }
    }
}
