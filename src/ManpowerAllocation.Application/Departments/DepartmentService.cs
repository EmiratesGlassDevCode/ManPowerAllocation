using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Departments;

/// <summary>
/// Default implementation of <see cref="IDepartmentService"/>. Every state-changing
/// method writes an audit entry in the same transaction as the change, so a department
/// mutation cannot be persisted without a corresponding audit record.
/// </summary>
public sealed class DepartmentService : IDepartmentService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal, used for server-side authorization.</param>
    public DepartmentService(IApplicationDbContext dbContext, IAuditWriter auditWriter, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentDto>> GetByDivisionAsync(Division division, CancellationToken cancellationToken = default)
    {
        // Projected inline (not via ToDto) so the query is fully translatable to SQL.
        return await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.Division == division)
            .OrderBy(d => d.Sequence)
            .ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id, d.Division, d.Name, d.RequiredDay, d.RequiredNight, d.Sequence, d.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(UserRole.Admin);

        var name = DepartmentName.Normalize(request.Name);
        if (name.Length == 0)
        {
            throw new BusinessRuleException("Department name is required.");
        }

        // A department name belongs to exactly one division and must be unique within it.
        var exists = await _dbContext.Departments
            .AnyAsync(d => d.Division == request.Division && d.Name == name, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException($"A department named '{name}' already exists in this division.");
        }

        var department = new Department
        {
            Division = request.Division,
            Name = name,
            RequiredDay = request.RequiredDay,
            RequiredNight = request.RequiredNight,
            Sequence = request.Sequence,
            IsActive = true
        };

        // Insert then audit inside one transaction: the department is saved first so its
        // generated key can be recorded, and the audit row is committed together with it.
        try
        {
            await _dbContext.ExecuteInTransactionAsync(async ct =>
            {
                _dbContext.Departments.Add(department);
                await _dbContext.SaveChangesAsync(ct);

                _auditWriter.Add(AuditAction.Create, nameof(Department), department.Id.ToString(), null, ToDto(department));
                await _dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent create of the same (Division, Name) won the unique-index race.
            throw new BusinessRuleException($"A department named '{name}' already exists in this division.");
        }

        return ToDto(department);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> UpdateAsync(int departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Require(UserRole.Admin);

        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), departmentId);

        var before = ToDto(department);

        department.RequiredDay = request.RequiredDay;
        department.RequiredNight = request.RequiredNight;
        department.Sequence = request.Sequence;
        department.IsActive = request.IsActive;

        _auditWriter.Add(AuditAction.Update, nameof(Department), department.Id.ToString(), before, ToDto(department));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(department);
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> SetActiveAsync(int departmentId, bool isActive, CancellationToken cancellationToken = default)
    {
        Require(UserRole.User);

        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), departmentId);

        var before = ToDto(department);
        department.IsActive = isActive;

        _auditWriter.Add(AuditAction.Update, nameof(Department), department.Id.ToString(), before, ToDto(department));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(department);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), departmentId);

        // Refuse to orphan employees: a department must be emptied before it can be removed.
        var hasEmployees = await _dbContext.Employees.AnyAsync(e => e.DepartmentId == departmentId, cancellationToken);
        if (hasEmployees)
        {
            throw new BusinessRuleException("Cannot delete a department that still has employees allocated to it.");
        }

        var before = ToDto(department);
        _dbContext.Departments.Remove(department);

        _auditWriter.Add(AuditAction.Delete, nameof(Department), departmentId.ToString(), before, null);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentDto>> GetEmptyDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        return await _dbContext.Departments
            .AsNoTracking()
            .Where(d => !_dbContext.Employees.Any(e => e.DepartmentId == d.Id))
            .OrderBy(d => d.Division)
            .ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id, d.Division, d.Name, d.RequiredDay, d.RequiredNight, d.Sequence, d.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentCleanupResult> DeleteEmptyDepartmentsAsync(IReadOnlyCollection<int> departmentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(departmentIds);
        Require(UserRole.Admin);

        var deleted = 0;
        var skipped = 0;
        var notFound = 0;

        var ids = departmentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new DepartmentCleanupResult(0, 0, 0);
        }

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Reset accumulators so a transient-failure retry does not double-count.
            deleted = 0;
            skipped = 0;
            notFound = 0;

            var departments = await _dbContext.Departments
                .Where(d => ids.Contains(d.Id))
                .ToListAsync(ct);
            var found = departments.ToDictionary(d => d.Id);

            foreach (var id in ids)
            {
                if (!found.TryGetValue(id, out var department))
                {
                    notFound++;
                    continue;
                }

                // Re-check under the transaction: never orphan employees, even if staff were
                // assigned between listing the empties and confirming the delete.
                var hasEmployees = await _dbContext.Employees.AnyAsync(e => e.DepartmentId == id, ct);
                if (hasEmployees)
                {
                    skipped++;
                    continue;
                }

                _dbContext.Departments.Remove(department);
                _auditWriter.Add(AuditAction.Delete, nameof(Department), id.ToString(), ToDto(department), null);
                deleted++;
            }

            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return new DepartmentCleanupResult(deleted, skipped, notFound);
    }

    /// <summary>
    /// Server-side authorization guard applied to every mutation, independent of the caller
    /// (Minimal API or in-process Blazor). Throws when the current principal lacks the role.
    /// </summary>
    private void Require(UserRole minimumRole)
    {
        if (!_currentUser.HasAtLeast(minimumRole))
        {
            throw new ForbiddenException();
        }
    }

    /// <summary>Projects a department entity to its transport representation.</summary>
    private static DepartmentDto ToDto(Department d) =>
        new(d.Id, d.Division, d.Name, d.RequiredDay, d.RequiredNight, d.Sequence, d.IsActive);
}
