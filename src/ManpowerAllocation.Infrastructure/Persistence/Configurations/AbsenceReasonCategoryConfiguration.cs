using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AbsenceReasonCategory"/>.</summary>
public sealed class AbsenceReasonCategoryConfiguration : IEntityTypeConfiguration<AbsenceReasonCategory>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<AbsenceReasonCategory> builder)
    {
        builder.ToTable("AbsenceReasonCategories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Kind).IsRequired();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Sequence).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();

        // Name is unique within a kind so an admin cannot create two identical categories.
        builder.HasIndex(c => new { c.Kind, c.Name }).IsUnique();
    }
}
