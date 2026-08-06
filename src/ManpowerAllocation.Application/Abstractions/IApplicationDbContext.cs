using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// The persistence contract the application layer depends on. Implemented by the
/// EF Core <c>ManpowerDbContext</c> in the infrastructure layer. All access goes
/// through EF Core (parameterised queries) — there is no raw SQL anywhere.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>Departments and their shift requirements.</summary>
    DbSet<Department> Departments { get; }

    /// <summary>Employees allocated to departments.</summary>
    DbSet<Employee> Employees { get; }

    /// <summary>Entra object id → role assignments.</summary>
    DbSet<RoleAssignment> RoleAssignments { get; }

    /// <summary>The immutable audit trail.</summary>
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    /// <summary>The single emergency break-glass account row.</summary>
    DbSet<BreakGlassAccount> BreakGlassAccounts { get; }

    /// <summary>The single admin-configurable shift-definition row.</summary>
    DbSet<ShiftSetting> ShiftSettings { get; }

    /// <summary>Named shift schedules (day/night windows + grace) that departments follow.</summary>
    DbSet<ShiftSchedule> ShiftSchedules { get; }

    /// <summary>The single admin-configurable email/SMTP configuration row.</summary>
    DbSet<EmailSettings> EmailSettings { get; }

    /// <summary>Captured daily report snapshots (one per operational date and shift).</summary>
    DbSet<AllocationSnapshot> AllocationSnapshots { get; }

    /// <summary>Per-department rows of captured snapshots.</summary>
    DbSet<AllocationSnapshotDepartment> AllocationSnapshotDepartments { get; }

    /// <summary>Per-employee allocation lines of captured snapshots.</summary>
    DbSet<AllocationSnapshotEmployee> AllocationSnapshotEmployees { get; }

    /// <summary>Persists all pending changes.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the supplied work inside a single database transaction, committing on success
    /// and rolling back on failure. Used where an operation must save more than once (for
    /// example, inserting a record and then writing its audit entry using the generated key)
    /// so that the change and its audit row are always committed together — never one without
    /// the other.
    /// </summary>
    /// <param name="work">The work to execute transactionally.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
