using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Employees;

/// <summary>An employee as returned to callers.</summary>
public sealed record EmployeeDto(
    int Id,
    string Name,
    string? BadgeNumber,
    Division Division,
    int DepartmentId,
    string DepartmentName,
    ShiftType Shift,
    AttendanceStatus Status,
    bool IsSupply,
    string? Notes);

/// <summary>Request to create an employee directly in the application.</summary>
public sealed record CreateEmployeeRequest
{
    /// <summary>Employee name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Badge / employee number.</summary>
    public string? BadgeNumber { get; init; }

    /// <summary>Owning department id.</summary>
    public int DepartmentId { get; init; }

    /// <summary>Assigned shift.</summary>
    public ShiftType Shift { get; init; }

    /// <summary>Initial attendance status.</summary>
    public AttendanceStatus Status { get; init; }

    /// <summary>Whether this is an outsource / supply worker.</summary>
    public bool IsSupply { get; init; }

    /// <summary>Free-text notes / designation.</summary>
    public string? Notes { get; init; }
}

/// <summary>Request to update an employee's editable details.</summary>
public sealed record UpdateEmployeeRequest
{
    /// <summary>Employee name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Badge / employee number.</summary>
    public string? BadgeNumber { get; init; }

    /// <summary>Free-text notes / designation.</summary>
    public string? Notes { get; init; }
}

/// <summary>Request to change an employee's attendance status.</summary>
public sealed record ChangeStatusRequest
{
    /// <summary>The new attendance status.</summary>
    public AttendanceStatus Status { get; init; }
}

/// <summary>Request to change an employee's shift.</summary>
public sealed record ChangeShiftRequest
{
    /// <summary>The new shift.</summary>
    public ShiftType Shift { get; init; }
}

/// <summary>Request to move an employee to another department within the same division.</summary>
public sealed record MoveEmployeeRequest
{
    /// <summary>The target department id. Must be in the same division as the employee.</summary>
    public int TargetDepartmentId { get; init; }
}

/// <summary>
/// Request to permanently reassign an employee to another department — a single-employee master
/// edit. Unlike a loan, this sets the employee's home department (and current department/division),
/// so a shift reset keeps them there. May cross divisions.
/// </summary>
public sealed record ReassignEmployeeRequest
{
    /// <summary>The new permanent (home) department id.</summary>
    public int TargetDepartmentId { get; init; }
}
