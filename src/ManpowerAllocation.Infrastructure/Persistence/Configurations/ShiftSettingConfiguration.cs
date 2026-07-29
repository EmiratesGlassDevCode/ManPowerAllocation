using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the single <see cref="ShiftSetting"/> row.</summary>
public sealed class ShiftSettingConfiguration : IEntityTypeConfiguration<ShiftSetting>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ShiftSetting> builder)
    {
        builder.ToTable("ShiftSettings");
        builder.HasKey(s => s.Id);

        // The key is assigned explicitly (the singleton id), never generated.
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.DayShiftStart).IsRequired();
        builder.Property(s => s.NightShiftStart).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedByObjectId).HasMaxLength(100);
    }
}
