using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AuditLogEntry"/>.</summary>
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).IsRequired().HasMaxLength(64);
        builder.Property(a => a.UserDisplayName).HasMaxLength(256);
        builder.Property(a => a.TimestampUtc).IsRequired();
        builder.Property(a => a.Action).IsRequired();
        builder.Property(a => a.EntityName).IsRequired().HasMaxLength(128);
        builder.Property(a => a.RecordId).HasMaxLength(64);
        builder.Property(a => a.OldValue);
        builder.Property(a => a.NewValue);
        builder.Property(a => a.IsBreakGlassSession).IsRequired();

        builder.HasIndex(a => a.TimestampUtc);
        builder.HasIndex(a => new { a.EntityName, a.RecordId });
        builder.HasIndex(a => a.IsBreakGlassSession);
    }
}
