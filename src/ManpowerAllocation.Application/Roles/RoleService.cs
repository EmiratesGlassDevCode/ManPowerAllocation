using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Roles;

/// <summary>
/// Default implementation of <see cref="IRoleService"/>. Guards against locking every
/// administrator out of the application, and audits each change.
/// </summary>
public sealed class RoleService : IRoleService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal, recorded as the creator of new assignments.</param>
    /// <param name="clock">Clock used for creation timestamps.</param>
    public RoleService(IApplicationDbContext dbContext, IAuditWriter auditWriter, ICurrentUser currentUser, IClock clock)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Server-side authorization guard for role administration. The internal
    /// <see cref="GetRoleAsync"/> lookup is intentionally exempt because it runs in a system
    /// context (the claims transformation) before a role claim exists.
    /// </summary>
    private void RequireAdmin()
    {
        if (!_currentUser.HasAtLeast(UserRole.Admin))
        {
            throw new ForbiddenException("Role administration requires the Admin role.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleAssignmentDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        return await _dbContext.RoleAssignments
            .AsNoTracking()
            .OrderBy(r => r.DisplayName)
            .Select(r => new RoleAssignmentDto(r.Id, r.EntraObjectId, r.DisplayName, r.Role, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserRole?> GetRoleAsync(string entraObjectId, CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.RoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EntraObjectId == entraObjectId, cancellationToken);
        return assignment?.Role;
    }

    /// <inheritdoc />
    public async Task<UserRole> EnsureDefaultViewerAsync(string entraObjectId, string? displayName, CancellationToken cancellationToken = default)
    {
        var normalisedObjectId = entraObjectId?.Trim();
        if (string.IsNullOrEmpty(normalisedObjectId))
        {
            return UserRole.Viewer;
        }

        var existing = await _dbContext.RoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EntraObjectId == normalisedObjectId, cancellationToken);

        if (existing is not null)
        {
            // Respect any role an administrator has already set (Viewer, User or Admin).
            return existing.Role;
        }

        var created = new RoleAssignment
        {
            EntraObjectId = normalisedObjectId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            Role = UserRole.Viewer,
            CreatedAtUtc = _clock.UtcNow,
            // Recorded as an automatic provisioning event rather than an administrator action.
            CreatedByObjectId = "auto-provision"
        };

        try
        {
            await _dbContext.ExecuteInTransactionAsync(async ct =>
            {
                _dbContext.RoleAssignments.Add(created);
                await _dbContext.SaveChangesAsync(ct);

                _auditWriter.Add(AuditAction.Create, nameof(RoleAssignment), created.Id.ToString(), null, ToDto(created));
                await _dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent login for the same principal won the race and inserted the row first;
            // the unique index rejected this one. That is fine — the user still ends up a Viewer.
            _dbContext.RoleAssignments.Remove(created);
            return UserRole.Viewer;
        }

        return UserRole.Viewer;
    }

    /// <inheritdoc />
    public async Task<RoleAssignmentDto> UpsertAsync(UpsertRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdmin();

        var normalisedObjectId = request.EntraObjectId.Trim();
        var existing = await _dbContext.RoleAssignments
            .FirstOrDefaultAsync(r => r.EntraObjectId == normalisedObjectId, cancellationToken);

        if (existing is null)
        {
            var created = new RoleAssignment
            {
                EntraObjectId = normalisedObjectId,
                DisplayName = request.DisplayName?.Trim(),
                Role = request.Role,
                CreatedAtUtc = _clock.UtcNow,
                CreatedByObjectId = _currentUser.UserId
            };

            try
            {
                await _dbContext.ExecuteInTransactionAsync(async ct =>
                {
                    _dbContext.RoleAssignments.Add(created);
                    await _dbContext.SaveChangesAsync(ct);

                    _auditWriter.Add(AuditAction.Create, nameof(RoleAssignment), created.Id.ToString(), null, ToDto(created));
                    await _dbContext.SaveChangesAsync(ct);
                }, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent upsert for the same object id won the unique-index race.
                throw new BusinessRuleException("That user was just assigned a role by someone else — reload and try again.");
            }

            return ToDto(created);
        }

        // Prevent demoting the last remaining administrator, which would lock everyone out.
        if (existing.Role == UserRole.Admin && request.Role != UserRole.Admin)
        {
            await GuardLastAdminAsync(existing.Id, cancellationToken);
        }

        var before = ToDto(existing);
        // Only overwrite the display name when one is supplied, so a role-only change does not wipe
        // a previously stored name.
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            existing.DisplayName = request.DisplayName.Trim();
        }
        existing.Role = request.Role;

        _auditWriter.Add(AuditAction.Update, nameof(RoleAssignment), existing.Id.ToString(), before, ToDto(existing));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int assignmentId, CancellationToken cancellationToken = default)
    {
        RequireAdmin();

        var assignment = await _dbContext.RoleAssignments
            .FirstOrDefaultAsync(r => r.Id == assignmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(RoleAssignment), assignmentId);

        if (assignment.Role == UserRole.Admin)
        {
            await GuardLastAdminAsync(assignment.Id, cancellationToken);
        }

        var before = ToDto(assignment);
        _dbContext.RoleAssignments.Remove(assignment);

        _auditWriter.Add(AuditAction.Delete, nameof(RoleAssignment), assignmentId.ToString(), before, null);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Throws when the supplied assignment is the only remaining Admin assignment.</summary>
    private async Task GuardLastAdminAsync(int assignmentId, CancellationToken cancellationToken)
    {
        var otherAdmins = await _dbContext.RoleAssignments
            .CountAsync(r => r.Role == UserRole.Admin && r.Id != assignmentId, cancellationToken);
        if (otherAdmins == 0)
        {
            throw new BusinessRuleException("At least one administrator must remain; this is the last Admin assignment.");
        }
    }

    /// <summary>Projects a role assignment entity to its transport representation.</summary>
    private static RoleAssignmentDto ToDto(RoleAssignment r) =>
        new(r.Id, r.EntraObjectId, r.DisplayName, r.Role, r.CreatedAtUtc);
}
