using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Employees;

/// <summary>Use-case operations for maintaining employees and recording their daily attendance.</summary>
public interface IEmployeeService
{
    /// <summary>Returns all employees in a division.</summary>
    /// <param name="division">The division to list.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<EmployeeDto>> GetByDivisionAsync(Division division, CancellationToken cancellationToken = default);

    /// <summary>Returns every employee across all divisions, ordered by division, department then name.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds employees whose name or badge number contains the search term (case-insensitive).</summary>
    /// <param name="term">The search term.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<EmployeeDto>> SearchAsync(string term, CancellationToken cancellationToken = default);

    /// <summary>Creates an employee in the specified department.</summary>
    /// <param name="request">The employee to create.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an employee's name, badge number and notes.</summary>
    /// <param name="employeeId">The employee to update.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmployeeDto> UpdateAsync(int employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Records a change to an employee's attendance status.</summary>
    /// <param name="employeeId">The employee whose status is changing.</param>
    /// <param name="request">The new status.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmployeeDto> ChangeStatusAsync(int employeeId, ChangeStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes an employee's shift.</summary>
    /// <param name="employeeId">The employee whose shift is changing.</param>
    /// <param name="request">The new shift.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmployeeDto> ChangeShiftAsync(int employeeId, ChangeShiftRequest request, CancellationToken cancellationToken = default);

    /// <summary>Moves an employee to another department within the same division.</summary>
    /// <param name="employeeId">The employee to move.</param>
    /// <param name="request">The target department.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<EmployeeDto> MoveAsync(int employeeId, MoveEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes an employee.</summary>
    /// <param name="employeeId">The employee to delete.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task DeleteAsync(int employeeId, CancellationToken cancellationToken = default);
}
