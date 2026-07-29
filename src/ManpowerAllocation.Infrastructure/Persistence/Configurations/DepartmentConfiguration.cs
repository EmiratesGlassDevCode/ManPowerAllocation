using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Department"/>.</summary>
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Division).IsRequired();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(120);
        builder.Property(d => d.RequiredDay).IsRequired();
        builder.Property(d => d.RequiredNight).IsRequired();
        builder.Property(d => d.Sequence).HasColumnType("decimal(9,2)");
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.RowVersion).IsRowVersion();

        // A department name belongs to exactly one division and is unique within it.
        builder.HasIndex(d => new { d.Division, d.Name }).IsUnique();

        builder.HasMany(d => d.Employees)
            .WithOne(e => e.Department!)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
