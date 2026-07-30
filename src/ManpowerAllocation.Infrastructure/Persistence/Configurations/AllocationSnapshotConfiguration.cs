using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AllocationSnapshot"/> and its detail rows.</summary>
public sealed class AllocationSnapshotConfiguration : IEntityTypeConfiguration<AllocationSnapshot>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<AllocationSnapshot> builder)
    {
        builder.ToTable("AllocationSnapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OperationalDate).IsRequired();
        builder.Property(s => s.Shift).IsRequired();
        builder.Property(s => s.CapturedAtUtc).IsRequired();
        builder.Property(s => s.Source).IsRequired().HasMaxLength(32);

        // One snapshot per operational date and shift; a re-capture replaces the previous one.
        builder.HasIndex(s => new { s.OperationalDate, s.Shift }).IsUnique();

        builder.HasMany(s => s.Departments)
            .WithOne(d => d.Snapshot!)
            .HasForeignKey(d => d.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Employees)
            .WithOne(e => e.Snapshot!)
            .HasForeignKey(e => e.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for <see cref="AllocationSnapshotDepartment"/>.</summary>
public sealed class AllocationSnapshotDepartmentConfiguration : IEntityTypeConfiguration<AllocationSnapshotDepartment>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<AllocationSnapshotDepartment> builder)
    {
        builder.ToTable("AllocationSnapshotDepartments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Division).IsRequired();
        builder.Property(d => d.DepartmentName).IsRequired().HasMaxLength(120);
        builder.Property(d => d.Status).IsRequired().HasMaxLength(16);

        builder.HasIndex(d => d.SnapshotId);
    }
}

/// <summary>EF Core mapping for <see cref="AllocationSnapshotEmployee"/>.</summary>
public sealed class AllocationSnapshotEmployeeConfiguration : IEntityTypeConfiguration<AllocationSnapshotEmployee>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<AllocationSnapshotEmployee> builder)
    {
        builder.ToTable("AllocationSnapshotEmployees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.BadgeNumber).HasMaxLength(50);
        builder.Property(e => e.Division).IsRequired();
        builder.Property(e => e.DepartmentName).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Shift).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.IsSupply).IsRequired();

        builder.HasIndex(e => e.SnapshotId);
    }
}
