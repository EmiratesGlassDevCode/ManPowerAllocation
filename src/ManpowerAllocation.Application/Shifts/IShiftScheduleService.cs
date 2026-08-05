namespace ManpowerAllocation.Application.Shifts;

/// <summary>
/// Manages the shift schedules (day/night windows + grace) and their assignment to departments.
/// All mutating operations require the Admin role and are audited.
/// </summary>
public interface IShiftScheduleService
{
    /// <summary>Lists all shift schedules.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<ShiftScheduleDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new shift schedule.</summary>
    /// <param name="request">The schedule to create.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ShiftScheduleDto> CreateAsync(CreateShiftScheduleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing shift schedule.</summary>
    /// <param name="id">The schedule to update.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ShiftScheduleDto> UpdateAsync(int id, UpdateShiftScheduleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists every department with the schedule it is currently assigned to.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<DepartmentScheduleDto>> GetDepartmentAssignmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Assigns a department to a shift schedule.</summary>
    /// <param name="departmentId">The department to reassign.</param>
    /// <param name="shiftScheduleId">The schedule to assign it to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task AssignAsync(int departmentId, int shiftScheduleId, CancellationToken cancellationToken = default);
}
