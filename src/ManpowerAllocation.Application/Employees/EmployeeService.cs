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
        Require(UserRole.User);

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
        Require(UserRole.User);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
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
        Require(UserRole.User);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
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
        Require(UserRole.User);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
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
        Require(UserRole.User);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
        var target = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == request.TargetDepartmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), request.TargetDepartmentId);

        // An employee may only move within their own division, matching the source behaviour.
        if (target.Division != employee.Division)
        {
            throw new BusinessRuleException("An employee can only be moved to a department in the same division.");
        }

        var before = ToDto(employee, employee.Department!.Name);
        employee.DepartmentId = target.Id;
        employee.Department = target;

        _auditWriter.Add(AuditAction.Update, nameof(Employee), employee.Id.ToString(), before, ToDto(employee, target.Name));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(employee, target.Name);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var employee = await LoadWithDepartmentAsync(employeeId, cancellationToken);
        var before = ToDto(employee, employee.Department!.Name);

        _dbContext.Employees.Remove(employee);

        _auditWriter.Add(AuditAction.Delete, nameof(Employee), employeeId.ToString(), before, null);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
