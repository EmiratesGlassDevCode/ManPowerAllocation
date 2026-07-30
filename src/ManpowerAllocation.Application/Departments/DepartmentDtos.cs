using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Departments;

/// <summary>A department as returned to callers.</summary>
public sealed record DepartmentDto(
    int Id,
    Division Division,
    string Name,
    int RequiredDay,
    int RequiredNight,
    decimal Sequence,
    bool IsActive);

/// <summary>Outcome of a bulk empty-department cleanup.</summary>
public sealed record DepartmentCleanupResult(int Deleted, int SkippedWithEmployees, int NotFound);

/// <summary>Request to create a department. The name is normalised server-side.</summary>
public sealed record CreateDepartmentRequest
{
    /// <summary>The division the department belongs to.</summary>
    public Division Division { get; init; }

    /// <summary>The department name (normalised to upper-case on save).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Required day-shift headcount.</summary>
    public int RequiredDay { get; init; }

    /// <summary>Required night-shift headcount.</summary>
    public int RequiredNight { get; init; }

    /// <summary>Display sequence within the division.</summary>
    public decimal Sequence { get; init; }
}

/// <summary>
/// Request to update a department. The name and division are immutable once created
/// (they are the join key), so only the requirement figures, sequence and active
/// state can change.
/// </summary>
public sealed record UpdateDepartmentRequest
{
    /// <summary>Required day-shift headcount.</summary>
    public int RequiredDay { get; init; }

    /// <summary>Required night-shift headcount.</summary>
    public int RequiredNight { get; init; }

    /// <summary>Display sequence within the division.</summary>
    public decimal Sequence { get; init; }

    /// <summary>Whether the department is switched ON.</summary>
    public bool IsActive { get; init; }
}
