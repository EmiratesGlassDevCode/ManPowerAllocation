using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Settings;

/// <summary>Default implementation of <see cref="IShiftSettingsService"/>. Every change is audited.</summary>
public sealed class ShiftSettingsService : IShiftSettingsService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal, recorded as the editor.</param>
    /// <param name="clock">Clock used for the change timestamp.</param>
    public ShiftSettingsService(IApplicationDbContext dbContext, IAuditWriter auditWriter, ICurrentUser currentUser, IClock clock)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ShiftSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Read without an authorization check: this also runs in the system context (attendance
        // sync) where no user role exists yet. Updates are the guarded operation.
        var settings = await _dbContext.ShiftSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ShiftSetting.SingletonId, cancellationToken);

        return settings is null
            ? new ShiftSettingsDto(new TimeSpan(7, 0, 0), new TimeSpan(19, 0, 0), default, null)
            : ToDto(settings);
    }

    /// <inheritdoc />
    public async Task<ShiftSettingsDto> UpdateAsync(UpdateShiftSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdmin();

        var settings = await _dbContext.ShiftSettings
            .FirstOrDefaultAsync(s => s.Id == ShiftSetting.SingletonId, cancellationToken);

        var before = settings is null ? null : ToDto(settings);

        if (settings is null)
        {
            settings = new ShiftSetting { Id = ShiftSetting.SingletonId };
            _dbContext.ShiftSettings.Add(settings);
        }

        settings.DayShiftStart = request.DayShiftStart;
        settings.NightShiftStart = request.NightShiftStart;
        settings.UpdatedAtUtc = _clock.UtcNow;
        settings.UpdatedByObjectId = _currentUser.UserId;

        _auditWriter.Add(AuditAction.Update, nameof(ShiftSetting), ShiftSetting.SingletonId.ToString(), before, ToDto(settings));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(settings);
    }

    private void RequireAdmin()
    {
        if (!_currentUser.HasAtLeast(UserRole.Admin))
        {
            throw new ForbiddenException("Changing shift settings requires the Admin role.");
        }
    }

    private static ShiftSettingsDto ToDto(ShiftSetting s) =>
        new(s.DayShiftStart, s.NightShiftStart, s.UpdatedAtUtc, s.UpdatedByObjectId);
}
