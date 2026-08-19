using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

// Server-side role enforcement is applied to every mutation below, independent of caller.

namespace ManpowerAllocation.Application.Employees;

/// <summary>
/// Default implementation of <see cref="IEmployeeService"/>. Every mutation writes an
/// audit entry alongside the change so no attendance edit is ever persisted silently.
/// </summary>
public sealed class EmployeeService : IEmployeeService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="auditWriter">Writer used to record every change in the audit trail.</param>
    /// <param name="currentUser">The current principal, used for server-side authorization.</param>
    public EmployeeService(IApplicationDbContext dbContext, IAuditWriter auditWriter, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
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

    /// <summary>
    /// Authorises an edit that targets a specific department: a User or Admin may edit any
    /// department; a department head may edit only the departments assigned to them. Enforced
    /// server-side so it cannot be bypassed by calling the API directly.
    /// </summary>
    private async Task RequireDepartmentEditAsync(int departmentId, CancellationToken cancellationToken)
    {
        if (_currentUser.HasAtLeast(UserRole.User)
            || await DepartmentScope.IsHeadOfAsync(_dbContext, _currentUser, departmentId, cancellationToken))
        {
            return;
        }

        throw new ForbiddenException();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeDto>> GetByDivisionAsync(Division division, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Division == division)
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeDto(
                e.Id, e.Name, e.BadgeNumber, e.Division, e.DepartmentId,
                e.Department!.Name, e.Shift, e.Status, e.IsSupply, e.Notes))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .OrderBy(e => e.Division)
            .ThenBy(e => e.Department!.Name)
            .ThenBy(e => e.Name)
            .Select(e => new EmployeeDto(
                e.Id, e.Name, e.BadgeNumber, e.Division, e.DepartmentId,
                e.Department!.Name, e.Shift, e.Status, e.IsSupply, e.Notes))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeDto>> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var trimmed = (term ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Array.Empty<EmployeeDto>();
        }

        // EF Core translates Contains to a parameterised LIKE; no string concatenation is used.
        return await _dbContext.Employees
            .AsNoTracking()
            .Where(e => EF.Functions.Like(e.Name, $"%{trimmed}%")
                        || (e.BadgeNumber != null && EF.Functions.Like(e.BadgeNumber, $"%{trimmed}%")))
            .OrderBy(e => e.Name)
            .Take(100)
            .Select(e => new EmployeeDto(
                e.Id, e.Name, e.BadgeNumber, e.Division, e.DepartmentId,
                e.Department!.Name, e.Shift, e.Status, e.IsSupply, e.Notes))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await RequireDepartmentEditAsync(request.DepartmentId, cancellationToken);

        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.DepartmentId);

        var employee = new Employee
        {
            Name = request.Name.Trim(),
            BadgeNumber = string.IsNullOrWhiteSpace(request.BadgeNumber) ? null : request.BadgeNumber.Trim(),
            DepartmentId = department.Id,
            // The division is derived from the department so the two can never disagree.
            Division = department.Division,
            Shift = request.Shift,
            Status = request.Status,
            IsSupply = request.IsSupply,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync(ct);

            _auditWriter.Add(AuditAction.Create, nameof(Employee), employee.Id.ToString(), null, ToDto(employee, department.Name));
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return ToDto(employee, department.Name);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> UpdateAsync(int employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
        await RequireDepartmentEditAsync(employee.DepartmentId, cancellationToken);
        var before = ToDto(employee, employee.Department!.Name);

        employee.Name = request.Name.Trim();
        employee.BadgeNumber = string.IsNullOrWhiteSpace(request.BadgeNumber) ? null : request.BadgeNumber.Trim();
        employee.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        _auditWriter.Add(AuditAction.Update, nameof(Employee), employee.Id.ToString(), before, ToDto(employee, employee.Department!.Name));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(employee, employee.Department!.Name);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> ChangeStatusAsync(int employeeId, ChangeStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
        await RequireDepartmentEditAsync(employee.DepartmentId, cancellationToken);
        var before = ToDto(employee, employee.Department!.Name);

        employee.Status = request.Status;

        _auditWriter.Add(AuditAction.Update, nameof(Employee), employee.Id.ToString(), before, ToDto(employee, employee.Department!.Name));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(employee, employee.Department!.Name);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> ChangeShiftAsync(int employeeId, ChangeShiftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
        await RequireDepartmentEditAsync(employee.DepartmentId, cancellationToken);
        var before = ToDto(employee, employee.Department!.Name);

        employee.Shift = request.Shift;

        _auditWriter.Add(AuditAction.Update, nameof(Employee), employee.Id.ToString(), before, ToDto(employee, employee.Department!.Name));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(employee, employee.Department!.Name);
    }

    /// <inheritdoc />
    public async Task<EmployeeDto> MoveAsync(int employeeId, MoveEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
        var source = employee.Department!;
        var target = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == request.TargetDepartmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.TargetDepartmentId);

        // A loan is a temporary reassignment (the permanent home changes only via the master import;
        // the shift-reset returns the person to their home). Both sides must be authorised: a
        // User/Admin passes anywhere; a department head passes their own departments and any shared
        // pool (Excess/Outsource).
        await RequireLoanSideAsync(source, cancellationToken);
        await RequireLoanSideAsync(target, cancellationToken);

        // Loans stay within a division unless a shared pool is involved — pools are factory-wide, so
        // picking from / returning to a pool may cross divisions.
        if (target.Division != employee.Division && !source.IsPool && !target.IsPool)
        {
            throw new BusinessRuleException("A loan can only cross divisions through a shared pool (Excess / Outsource).");
        }

        var before = ToDto(employee, source.Name);
        employee.DepartmentId = target.Id;
        employee.Department = target;
        // Keep the employee's division aligned with where they are working for the shift, so the
        // dashboards and rosters show them under the loaned department/division consistently.
        employee.Division = target.Division;

        _auditWriter.Add(AuditAction.Update, nameof(Employee), employee.Id.ToString(), before, ToDto(employee, target.Name));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(employee, target.Name);
    }

    /// <summary>
    /// Authorises one side of a loan: a User/Admin may use any department; a department head may use
    /// their own departments and any shared pool (Excess/Outsource); anyone else is refused.
    /// </summary>
    private async Task RequireLoanSideAsync(Department department, CancellationToken cancellationToken)
    {
        if (_currentUser.HasAtLeast(UserRole.User))
        {
            return;
        }

        if (department.IsPool && _currentUser.Role == UserRole.DepartmentHead)
        {
            return;
        }

        if (await DepartmentScope.IsHeadOfAsync(_dbContext, _currentUser, department.Id, cancellationToken))
        {
            return;
        }

        throw new ForbiddenException();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        // Read the before-image WITHOUT tracking. The context is scoped to the (long-lived) Blazor
        // circuit, so a copy of this employee tracked earlier in the session — e.g. after opening its
        // Edit dialog — could otherwise supply a stale RowVersion and make the delete fail with a
        // spurious "0 rows affected" concurrency error.
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        await RequireDepartmentEditAsync(employee.DepartmentId, cancellationToken);
        var before = ToDto(employee, employee.Department!.Name);

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Delete directly by id. This does not depend on a tracked entity's RowVersion, so it
            // cannot fail with a stale-token concurrency error; deletion is idempotent by intent.
            await _dbContext.Employees.Where(e => e.Id == employeeId).ExecuteDeleteAsync(ct);

            _auditWriter.Add(AuditAction.Delete, nameof(Employee), employeeId.ToString(), before, null);
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <summary>Loads a tracked employee together with its department, or throws if it does not exist.</summary>
    private async Task<Employee> LoadWithDepartmentAsync(int employeeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), employeeId);
    }

    /// <summary>Projects an employee to a DTO using an explicitly supplied department name.</summary>
    private static EmployeeDto ToDto(Employee e, string departmentName) => new(
        e.Id,
        e.Name,
        e.BadgeNumber,
        e.Division,
        e.DepartmentId,
        departmentName,
        e.Shift,
        e.Status,
        e.IsSupply,
        e.Notes);
}
