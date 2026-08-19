using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Infrastructure.Persistence;

/// <summary>
/// The EF Core persistence context for the application. Implements
/// <see cref="IApplicationDbContext"/> so the application layer depends only on the
/// abstraction. All access is through EF Core; there is no raw SQL anywhere.
/// </summary>
public sealed class ManpowerDbContext : DbContext, IApplicationDbContext
{
    /// <summary>Initialises the context with the supplied options.</summary>
    /// <param name="options">The context options (provider, connection string, etc.).</param>
    public ManpowerDbContext(DbContextOptions<ManpowerDbContext> options) : base(options)
    {
    }

    /// <inheritdoc />
    public DbSet<Department> Departments => Set<Department>();

    /// <inheritdoc />
    public DbSet<Employee> Employees => Set<Employee>();

    /// <inheritdoc />
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    /// <inheritdoc />
    public DbSet<DepartmentManager> DepartmentManagers => Set<DepartmentManager>();

    /// <inheritdoc />
    public DbSet<AbsenceReasonCategory> AbsenceReasonCategories => Set<AbsenceReasonCategory>();

    /// <inheritdoc />
    public DbSet<EmployeeAbsence> EmployeeAbsences => Set<EmployeeAbsence>();

    /// <inheritdoc />
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    /// <inheritdoc />
    public DbSet<BreakGlassAccount> BreakGlassAccounts => Set<BreakGlassAccount>();

    /// <inheritdoc />
    public DbSet<ShiftSetting> ShiftSettings => Set<ShiftSetting>();

    /// <inheritdoc />
    public DbSet<AllocationSettings> AllocationSettings => Set<AllocationSettings>();

    /// <inheritdoc />
    public DbSet<ShiftSchedule> ShiftSchedules => Set<ShiftSchedule>();

    /// <inheritdoc />
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();

    /// <inheritdoc />
    public DbSet<AllocationSnapshot> AllocationSnapshots => Set<AllocationSnapshot>();

    /// <inheritdoc />
    public DbSet<AllocationSnapshotDepartment> AllocationSnapshotDepartments => Set<AllocationSnapshotDepartment>();

    /// <inheritdoc />
    public DbSet<AllocationSnapshotEmployee> AllocationSnapshotEmployees => Set<AllocationSnapshotEmployee>();

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        // A retrying execution strategy requires the transactional unit to be wrapped so the
        // whole block can be replayed safely on a transient failure.
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <summary>Applies all entity configurations in this assembly.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ManpowerDbContext).Assembly);
    }
}
