using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the singleton <see cref="EmailSettings"/> row.</summary>
public sealed class EmailSettingsConfiguration : IEntityTypeConfiguration<EmailSettings>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<EmailSettings> builder)
    {
        builder.ToTable("EmailSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Mode).IsRequired();
        builder.Property(e => e.Security).IsRequired();
        builder.Property(e => e.Host).HasMaxLength(256);
        builder.Property(e => e.FromAddress).HasMaxLength(256);
        builder.Property(e => e.FromName).HasMaxLength(128);
        builder.Property(e => e.Username).HasMaxLength(256);
        builder.Property(e => e.PasswordProtected).HasMaxLength(4096);
        builder.Property(e => e.TenantId).HasMaxLength(128);
        builder.Property(e => e.ClientId).HasMaxLength(128);
        builder.Property(e => e.ClientSecretProtected).HasMaxLength(4096);
        builder.Property(e => e.SenderMailbox).HasMaxLength(256);
        builder.Property(e => e.Recipients).HasMaxLength(4000);
        builder.Property(e => e.Cc).HasMaxLength(4000);
        builder.Property(e => e.Bcc).HasMaxLength(4000);
        builder.Property(e => e.ReplyTo).HasMaxLength(256);
    }
}
