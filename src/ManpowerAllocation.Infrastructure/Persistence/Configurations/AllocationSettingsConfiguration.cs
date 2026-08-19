using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the single <see cref="AllocationSettings"/> row.</summary>
public sealed class AllocationSettingsConfiguration : IEntityTypeConfiguration<AllocationSettings>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<AllocationSettings> builder)
    {
        builder.ToTable("AllocationSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.AutoShiftResetEnabled).IsRequired();
        builder.Property(s => s.LastMasterUploadUtc);
        builder.Property(s => s.LastResetMarker).HasMaxLength(32);
        builder.Property(s => s.LastResetAtUtc);
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedByObjectId).HasMaxLength(100);
    }
}
