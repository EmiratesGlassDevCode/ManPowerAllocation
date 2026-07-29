using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="RoleAssignment"/>.</summary>
public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.EntraObjectId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.DisplayName).HasMaxLength(256);
        builder.Property(r => r.Role).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.CreatedByObjectId).HasMaxLength(64);

        // One role per Entra principal.
        builder.HasIndex(r => r.EntraObjectId).IsUnique();
    }
}
