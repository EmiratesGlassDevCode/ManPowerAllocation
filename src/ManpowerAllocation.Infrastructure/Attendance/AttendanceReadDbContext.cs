using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// A dedicated, read-only EF Core context over the external attendance database on the same
/// server. It maps only the attendance view and is never migrated or written to, keeping the
/// governed application database and this external source cleanly separated.
/// </summary>
public sealed class AttendanceReadDbContext : DbContext
{
    /// <summary>Initialises the context with the supplied options.</summary>
    /// <param name="options">The context options (connection string to the attendance database).</param>
    public AttendanceReadDbContext(DbContextOptions<AttendanceReadDbContext> options) : base(options)
    {
    }

    /// <summary>The attendance view rows.</summary>
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    /// <summary>Maps the keyless entity to the external view and its exact column names.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("xxeg_attendance_v", "dbo");
            entity.Property(r => r.EmployeeId).HasColumnName("Employee ID");
            entity.Property(r => r.Dt).HasColumnName("Dt");
            entity.Property(r => r.InTime).HasColumnName("Intime");
            entity.Property(r => r.OutTime).HasColumnName("OutTime");
        });
    }
}
