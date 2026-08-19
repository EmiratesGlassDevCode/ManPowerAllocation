using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Absences;

/// <summary>
/// Drives the Absentees page: it lists employees who are currently absent (per the attendance sync)
/// together with anyone on an active Informed leave, and records/edits the reason for each. Reads are
/// available to any viewer; recording a reason is restricted to a User/Admin, or a department head
/// acting within their own departments — enforced server-side.
/// </summary>
public interface IAbsenceService
{
    /// <summary>
    /// Lists the current absentees. Includes every employee whose attendance status is Absent, plus
    /// anyone with an active Informed absence (planned leave covering today), optionally filtered to a
    /// single division. Each row carries the active reason, if one has been recorded.
    /// </summary>
    /// <param name="division">Optional division filter; null returns all divisions.</param>
    /// <param name="asOf">Optional operational date to view; a past date returns the captured snapshot (read-only). Null/today = live.</param>
    /// <param name="shift">Optional shift filter (Day/Night); null returns both.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<AbsenceListItemDto>> GetAbsenteesAsync(Division? division, DateOnly? asOf = null, ShiftType? shift = null, CancellationToken cancellationToken = default);

    /// <summary>Sets (creates or replaces) the current absence reason for an employee. Scope-checked.</summary>
    Task SetReasonAsync(SetAbsenceReasonRequest request, CancellationToken cancellationToken = default);

    /// <summary>Clears the current/planned absence reason for an employee. Scope-checked.</summary>
    Task ClearReasonAsync(int employeeId, CancellationToken cancellationToken = default);
}
