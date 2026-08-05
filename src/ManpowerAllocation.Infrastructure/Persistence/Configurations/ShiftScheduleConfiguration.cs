using ManpowerAllocation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManpowerAllocation.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ShiftSchedule"/>. Two schedules are seeded: schedule 1
/// (07:00–19:00) is the default assigned to every department, and schedule 2 (06:00–18:00) is
/// available for departments that run an hour earlier.
/// </summary>
public sealed class ShiftScheduleConfiguration : IEntityTypeConfiguration<ShiftSchedule>
{
    /// <summary>Configures the entity.</summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ShiftSchedule> builder)
    {
        builder.ToTable("ShiftSchedules");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(80);
        builder.Property(s => s.DayStart).IsRequired();
        builder.Property(s => s.GraceMinutes).IsRequired();

        builder.HasData(
            new ShiftSchedule { Id = 1, Name = "07:00 – 19:00", DayStart = new TimeSpan(7, 0, 0), GraceMinutes = 60 },
            new ShiftSchedule { Id = 2, Name = "06:00 – 18:00", DayStart = new TimeSpan(6, 0, 0), GraceMinutes = 60 });
    }
}
