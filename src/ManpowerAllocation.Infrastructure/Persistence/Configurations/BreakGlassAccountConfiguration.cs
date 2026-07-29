using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="BreakGlassAccount"/>.</summary>
public sealed class BreakGlassAccountConfiguration : IEntityTypeConfiguration<BreakGlassAccount>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<BreakGlassAccount> builder)
    {
        builder.ToTable("BreakGlassAccounts");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.UserName).IsRequired().HasMaxLength(64);
        builder.Property(b => b.IsEnabled).IsRequired();
        builder.Property(b => b.EnableReason).HasMaxLength(500);

        // Concurrency token so a manual enable in the database and an auto-disable by the
        // background worker cannot silently overwrite one another.
        builder.Property(b => b.RowVersion).IsRowVersion();

        builder.HasIndex(b => b.UserName).IsUnique();

        // Note: this row deliberately stores NO credential. The break-glass secret hash lives
        // in protected configuration, so the database never holds a password of any kind.
    }
}
