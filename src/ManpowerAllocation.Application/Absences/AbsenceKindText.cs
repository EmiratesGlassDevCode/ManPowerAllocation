using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Absences;

/// <summary>Presentation helpers for <see cref="AbsenceKind"/>, shared across services, pages and exports.</summary>
public static class AbsenceKindText
{
    /// <summary>The display label for a kind (e.g. "Not Informed").</summary>
    public static string Label(AbsenceKind kind) => kind switch
    {
        AbsenceKind.Informed => "Informed",
        AbsenceKind.NotInformed => "Not Informed",
        AbsenceKind.Vacation => "Vacation",
        _ => kind.ToString()
    };

    /// <summary>True for kinds that carry a From/To date range (Informed and Vacation).</summary>
    public static bool HasDateRange(AbsenceKind kind) => kind is AbsenceKind.Informed or AbsenceKind.Vacation;
}
