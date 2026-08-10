using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="DepartmentManager"/> (department-head scope).</summary>
public sealed class DepartmentManagerConfiguration : IEntityTypeConfiguration<DepartmentManager>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DepartmentManager> builder)
    {
        builder.ToTable("DepartmentManagers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.EntraObjectId).IsRequired().HasMaxLength(64);
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.CreatedByObjectId).HasMaxLength(64);

        builder.HasOne(m => m.Department)
            .WithMany()
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // A head is assigned to a department at most once.
        builder.HasIndex(m => new { m.EntraObjectId, m.DepartmentId }).IsUnique();
    }
}
