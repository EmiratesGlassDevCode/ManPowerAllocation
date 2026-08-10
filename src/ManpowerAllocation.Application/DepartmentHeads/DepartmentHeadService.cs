using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.DepartmentHeads;

/// <summary>
/// Default <see cref="IDepartmentHeadService"/>. Appointing a head sets their role to
/// <see cref="UserRole.DepartmentHead"/> and replaces their managed-department set atomically; every
/// change is audited. All mutating operations require the Admin role.
/// </summary>
public sealed class DepartmentHeadService : IDepartmentHeadService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal (authorization + auditing + own-scope lookup).</param>
    /// <param name="clock">Clock used for creation timestamps.</param>
    public DepartmentHeadService(IApplicationDbContext dbContext, IAuditWriter auditWriter, ICurrentUser currentUser, IClock clock)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentHeadDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var rows = await _dbContext.DepartmentManagers
            .AsNoTracking()
            .Include(m => m.Department)
            .ToListAsync(cancellationToken);

        var names = await _dbContext.RoleAssignments
            .AsNoTracking()
            .ToDictionaryAsync(r => r.EntraObjectId, r => r.DisplayName, cancellationToken);

        return rows
            .GroupBy(m => m.EntraObjectId)
            .Select(g => new DepartmentHeadDto(
                g.Key,
                names.TryGetValue(g.Key, out var dn) ? dn : null,
                g.Where(m => m.Department is not null)
                    .Select(m => new DepartmentHeadScopeDto(m.DepartmentId, m.Department!.Name, m.Department!.Division))
                    .OrderBy(d => d.DepartmentName)
                    .ToList()))
            .OrderBy(h => h.DisplayName ?? h.EntraObjectId)
            .ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(UpsertDepartmentHeadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(UserRole.Admin);

        var objectId = request.EntraObjectId?.Trim();
        if (string.IsNullOrEmpty(objectId))
        {
            throw new BusinessRuleException("An Entra object id is required.");
        }

        var departmentIds = request.DepartmentIds.Distinct().ToList();
        if (departmentIds.Count == 0)
        {
            // No departments selected — this is a revocation.
            await RevokeAsync(objectId, cancellationToken);
            return;
        }

        var existingCount = await _dbContext.Departments.CountAsync(d => departmentIds.Contains(d.Id), cancellationToken);
        if (existingCount != departmentIds.Count)
        {
            throw new BusinessRuleException("One or more selected departments do not exist.");
        }

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var assignment = await _dbContext.RoleAssignments.FirstOrDefaultAsync(r => r.EntraObjectId == objectId, ct);
            if (assignment is null)
            {
                _dbContext.RoleAssignments.Add(new RoleAssignment
                {
                    EntraObjectId = objectId,
                    DisplayName = Normalize(request.DisplayName),
                    Role = UserRole.DepartmentHead,
                    CreatedAtUtc = _clock.UtcNow,
                    CreatedByObjectId = _currentUser.UserId
                });
            }
            else
            {
                assignment.Role = UserRole.DepartmentHead;
                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    assignment.DisplayName = request.DisplayName.Trim();
                }
            }

            var current = await _dbContext.DepartmentManagers.Where(m => m.EntraObjectId == objectId).ToListAsync(ct);
            _dbContext.DepartmentManagers.RemoveRange(current);

            foreach (var deptId in departmentIds)
            {
                _dbContext.DepartmentManagers.Add(new DepartmentManager
                {
                    EntraObjectId = objectId,
                    DepartmentId = deptId,
                    CreatedAtUtc = _clock.UtcNow,
                    CreatedByObjectId = _currentUser.UserId
                });
            }

            _auditWriter.Add(AuditAction.Update, nameof(DepartmentManager), objectId,
                new { EntraObjectId = objectId, DepartmentIds = current.Select(m => m.DepartmentId).ToList() },
                new { EntraObjectId = objectId, DepartmentIds = departmentIds });

            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string entraObjectId, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var objectId = entraObjectId?.Trim();
        if (string.IsNullOrEmpty(objectId))
        {
            return;
        }

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var current = await _dbContext.DepartmentManagers.Where(m => m.EntraObjectId == objectId).ToListAsync(ct);
            if (current.Count > 0)
            {
                _dbContext.DepartmentManagers.RemoveRange(current);
            }

            // Reset the role to Viewer so a former head keeps read-only access, not elevated rights.
            var assignment = await _dbContext.RoleAssignments.FirstOrDefaultAsync(r => r.EntraObjectId == objectId, ct);
            if (assignment is not null && assignment.Role == UserRole.DepartmentHead)
            {
                assignment.Role = UserRole.Viewer;
            }

            _auditWriter.Add(AuditAction.Delete, nameof(DepartmentManager), objectId,
                new { EntraObjectId = objectId, DepartmentIds = current.Select(m => m.DepartmentId).ToList() }, null);

            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetManagedDepartmentIdsAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != UserRole.DepartmentHead || string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Array.Empty<int>();
        }

        return await _dbContext.DepartmentManagers
            .AsNoTracking()
            .Where(m => m.EntraObjectId == _currentUser.UserId)
            .Select(m => m.DepartmentId)
            .ToListAsync(cancellationToken);
    }

    private void Require(UserRole minimumRole)
    {
        if (!_currentUser.HasAtLeast(minimumRole))
        {
            throw new ForbiddenException();
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
