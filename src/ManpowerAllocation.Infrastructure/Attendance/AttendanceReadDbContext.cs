using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// A dedicated, read-only EF Core context over the external attendance database on the same
/// server. It maps only the attendance view and is never migrated or written to, keeping the
/// governed application database and this external source cleanly separated. The view name,
/// schema and column names are all configuration-driven (see <see cref="AttendanceOptions"/>),
/// so pointing at a renamed or restructured view never requires a code change or rebuild.
/// </summary>
public sealed class AttendanceReadDbContext : DbContext
{
    private readonly AttendanceOptions _options;

    /// <summary>Initialises the context with the supplied options.</summary>
    /// <param name="options">The context options (connection string to the attendance database).</param>
    /// <param name="attendanceOptions">The view/column mapping, read from configuration.</param>
    public AttendanceReadDbContext(DbContextOptions<AttendanceReadDbContext> options, IOptions<AttendanceOptions> attendanceOptions)
        : base(options)
    {
        _options = attendanceOptions.Value;
    }

    /// <summary>The attendance view rows.</summary>
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    /// <summary>Maps the keyless entity to the configured external view and column names.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(_options.ViewName, _options.ViewSchema);
            // SQL Server column names are case-insensitive, so the defaults below ("dt"/"InTime")
            // resolve fine as-is; override any of the Attendance:*Column settings if a
            // replacement view uses different column names.
            entity.Property(r => r.EmployeeId).HasColumnName(_options.EmployeeIdColumn);
            entity.Property(r => r.Dt).HasColumnName(_options.DateColumn);
            entity.Property(r => r.InTime).HasColumnName(_options.InTimeColumn);
            entity.Property(r => r.OutTime).HasColumnName(_options.OutTimeColumn);
        });
    }
}
