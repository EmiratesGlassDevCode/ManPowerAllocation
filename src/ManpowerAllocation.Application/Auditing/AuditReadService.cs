using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Auditing;

/// <summary>Default <see cref="IAuditReadService"/>. Admin-only, read-only projection of the audit trail.</summary>
public sealed class AuditReadService : IAuditReadService
{
    private const int MaxTake = 500;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="currentUser">The current principal, used for the Admin authorization check.</param>
    public AuditReadService(IApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take, bool breakGlassOnly, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasAtLeast(UserRole.Admin))
        {
            throw new ForbiddenException("Viewing the audit trail requires the Admin role.");
        }

        var limit = Math.Clamp(take, 1, MaxTake);

        var query = _dbContext.AuditLogEntries.AsNoTracking();
        if (breakGlassOnly)
        {
            query = query.Where(a => a.IsBreakGlassSession);
        }

        return await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, a.UserDisplayName, a.TimestampUtc, a.Action,
                a.EntityName, a.RecordId, a.OldValue, a.NewValue, a.IsBreakGlassSession))
            .ToListAsync(cancellationToken);
    }
}
