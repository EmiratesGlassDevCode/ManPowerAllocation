using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="MasterSnapshot"/> and its child lines.</summary>
public sealed class MasterSnapshotConfiguration : IEntityTypeConfiguration<MasterSnapshot>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<MasterSnapshot> builder)
    {
        builder.ToTable("MasterSnapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CapturedAtUtc).IsRequired();
        builder.Property(s => s.CapturedByObjectId).HasMaxLength(64);
        builder.Property(s => s.CapturedByName).HasMaxLength(256);
        builder.Property(s => s.Source).IsRequired().HasMaxLength(64);
        builder.Property(s => s.EmployeeCount).IsRequired();
        builder.Property(s => s.DepartmentCount).IsRequired();

        builder.HasIndex(s => s.CapturedAtUtc);

        builder.HasMany(s => s.Employees)
            .WithOne(e => e.Snapshot!)
            .HasForeignKey(e => e.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Departments)
            .WithOne(d => d.Snapshot!)
            .HasForeignKey(d => d.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF Core mapping for <see cref="MasterSnapshotEmployee"/>.</summary>
public sealed class MasterSnapshotEmployeeConfiguration : IEntityTypeConfiguration<MasterSnapshotEmployee>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<MasterSnapshotEmployee> builder)
    {
        builder.ToTable("MasterSnapshotEmployees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.BadgeNumber).HasMaxLength(50);
        builder.Property(e => e.HomeDepartmentName).IsRequired().HasMaxLength(120);

        builder.HasIndex(e => e.SnapshotId);
    }
}

/// <summary>EF Core mapping for <see cref="MasterSnapshotDepartment"/>.</summary>
public sealed class MasterSnapshotDepartmentConfiguration : IEntityTypeConfiguration<MasterSnapshotDepartment>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<MasterSnapshotDepartment> builder)
    {
        builder.ToTable("MasterSnapshotDepartments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DepartmentName).IsRequired().HasMaxLength(120);

        builder.HasIndex(d => d.SnapshotId);
    }
}
