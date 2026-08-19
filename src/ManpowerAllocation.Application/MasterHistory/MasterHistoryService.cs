using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.MasterHistory;

/// <summary>Default <see cref="IMasterHistoryService"/>. Admin-only, read-only.</summary>
public sealed class MasterHistoryService : IMasterHistoryService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    public MasterHistoryService(IApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MasterSnapshotSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasAtLeast(UserRole.Admin))
        {
            throw new ForbiddenException("Viewing the master history requires the Admin role.");
        }

        return await _dbContext.MasterSnapshots
            .AsNoTracking()
            .OrderByDescending(s => s.CapturedAtUtc)
            .Select(s => new MasterSnapshotSummaryDto(
                s.Id, s.CapturedAtUtc, s.CapturedByName, s.Source, s.EmployeeCount, s.DepartmentCount))
            .ToListAsync(cancellationToken);
    }
}
