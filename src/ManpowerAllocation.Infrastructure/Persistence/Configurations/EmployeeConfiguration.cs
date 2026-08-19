using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Employee"/>.</summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.BadgeNumber).HasMaxLength(50);
        builder.Property(e => e.Division).IsRequired();
        builder.Property(e => e.Shift).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.IsSupply).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Home department: a second, optional relationship to Department (no navigation, no cascade),
        // set by the master import. Restrict so a department in use as a home cannot be silently deleted.
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(e => e.HomeDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Division);
        builder.HasIndex(e => e.DepartmentId);
        builder.HasIndex(e => e.HomeDepartmentId);
        // Badge numbers are not unique (they repeat and outsource rows reuse values),
        // but an index still speeds up the employee search.
        builder.HasIndex(e => e.BadgeNumber);
    }
}
