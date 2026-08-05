using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Shifts;

/// <summary>
/// Default <see cref="IShiftScheduleService"/>. Reads and mutates the shift schedules and their
/// department assignments; every change requires the Admin role and writes an audit entry.
/// </summary>
public sealed class ShiftScheduleService : IShiftScheduleService
{
    private static readonly TimeSpan ShiftLength = TimeSpan.FromHours(12);

    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal, used for server-side authorization.</param>
    public ShiftScheduleService(IApplicationDbContext dbContext, IAuditWriter auditWriter, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShiftScheduleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _dbContext.ShiftSchedules
            .AsNoTracking()
            .OrderBy(s => s.DayStart)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return schedules.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ShiftScheduleDto> CreateAsync(CreateShiftScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(UserRole.Admin);
        Validate(request.Name, request.DayStart, request.GraceMinutes);

        var schedule = new ShiftSchedule
        {
            Name = request.Name.Trim(),
            DayStart = request.DayStart,
            GraceMinutes = request.GraceMinutes
        };

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            _dbContext.ShiftSchedules.Add(schedule);
            await _dbContext.SaveChangesAsync(ct);

            _auditWriter.Add(AuditAction.Create, nameof(ShiftSchedule), schedule.Id.ToString(), null, ToDto(schedule));
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return ToDto(schedule);
    }

    /// <inheritdoc />
    public async Task<ShiftScheduleDto> UpdateAsync(int id, UpdateShiftScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(UserRole.Admin);
        Validate(request.Name, request.DayStart, request.GraceMinutes);

        var schedule = await _dbContext.ShiftSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShiftSchedule), id);

        var before = ToDto(schedule);
        schedule.Name = request.Name.Trim();
        schedule.DayStart = request.DayStart;
        schedule.GraceMinutes = request.GraceMinutes;

        _auditWriter.Add(AuditAction.Update, nameof(ShiftSchedule), schedule.Id.ToString(), before, ToDto(schedule));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(schedule);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentScheduleDto>> GetDepartmentAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Departments
            .AsNoTracking()
            .OrderBy(d => d.Division)
            .ThenBy(d => d.Sequence)
            .ThenBy(d => d.Name)
            .Select(d => new DepartmentScheduleDto(
                d.Id,
                d.Name,
                d.Division,
                d.ShiftScheduleId,
                d.ShiftSchedule != null ? d.ShiftSchedule.Name : string.Empty))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AssignAsync(int departmentId, int shiftScheduleId, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var department = await _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), departmentId);

        var scheduleExists = await _dbContext.ShiftSchedules.AnyAsync(s => s.Id == shiftScheduleId, cancellationToken);
        if (!scheduleExists)
        {
            throw new NotFoundException(nameof(ShiftSchedule), shiftScheduleId);
        }

        if (department.ShiftScheduleId == shiftScheduleId)
        {
            return;
        }

        var before = new { department.Id, department.ShiftScheduleId };
        department.ShiftScheduleId = shiftScheduleId;

        _auditWriter.Add(AuditAction.Update, nameof(Department), department.Id.ToString(),
            before, new { department.Id, department.ShiftScheduleId });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(string name, TimeSpan dayStart, int graceMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("A schedule name is required.");
        }

        if (dayStart < TimeSpan.Zero || dayStart >= TimeSpan.FromHours(24))
        {
            throw new BusinessRuleException("The day-shift start must be a time of day.");
        }

        if (graceMinutes is < 0 or > 240)
        {
            throw new BusinessRuleException("The grace must be between 0 and 240 minutes.");
        }
    }

    private void Require(UserRole minimumRole)
    {
        if (!_currentUser.HasAtLeast(minimumRole))
        {
            throw new ForbiddenException();
        }
    }

    private static ShiftScheduleDto ToDto(ShiftSchedule s) =>
        new(s.Id, s.Name, s.DayStart, WrapToDay(s.DayStart + ShiftLength), s.GraceMinutes);

    private static TimeSpan WrapToDay(TimeSpan value)
    {
        var ticks = value.Ticks % TimeSpan.TicksPerDay;
        if (ticks < 0)
        {
            ticks += TimeSpan.TicksPerDay;
        }

        return TimeSpan.FromTicks(ticks);
    }
}
