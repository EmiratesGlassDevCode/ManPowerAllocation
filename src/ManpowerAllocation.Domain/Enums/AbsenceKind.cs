namespace ManpowerAllocation.Domain.Enums;

/// <summary>
/// The two fixed top-level reasons an employee can be absent. Each kind owns a set of
/// admin-managed sub-categories (<see cref="Entities.AbsenceReasonCategory"/>). An
/// <see cref="Informed"/> absence carries a From/To date range and expires (drops off the
/// current list) once its period ends; a <see cref="NotInformed"/> absence carries only a
/// free-text comment and applies until the employee returns or the reason is cleared.
/// </summary>
public enum AbsenceKind
{
    /// <summary>The absence was communicated in advance (e.g. planned leave). Has a date range.</summary>
    Informed = 1,

    /// <summary>The absence was not communicated. Has a comment only, no date range.</summary>
    NotInformed = 2
}
