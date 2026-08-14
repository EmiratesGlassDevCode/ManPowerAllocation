using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="EmployeeAbsence"/>.</summary>
public sealed class EmployeeAbsenceConfiguration : IEntityTypeConfiguration<EmployeeAbsence>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<EmployeeAbsence> builder)
    {
        builder.ToTable("EmployeeAbsences");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind).IsRequired();
        builder.Property(a => a.FromDate).IsRequired();
        builder.Property(a => a.ToDate);
        builder.Property(a => a.Comment).HasMaxLength(1000);
        builder.Property(a => a.CreatedByObjectId).IsRequired().HasMaxLength(64);
        builder.Property(a => a.CreatedByName).HasMaxLength(256);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reference the category but keep records when a category is deactivated/removed: restrict
        // delete so a used category cannot be hard-deleted out from under an absence record.
        builder.HasOne(a => a.Category)
            .WithMany()
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.EmployeeId);
        builder.HasIndex(a => new { a.FromDate, a.ToDate });
    }
}
