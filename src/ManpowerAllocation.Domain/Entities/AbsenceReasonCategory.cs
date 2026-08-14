using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// An admin-managed sub-category of an absence reason, belonging to one fixed
/// <see cref="AbsenceKind"/> (Informed or Not Informed). Administrators add, rename,
/// re-order and deactivate these from the Absence Categories admin screen; they populate
/// the reason drop-down on the Absentees page. Categories are deactivated rather than
/// deleted once used, so historical absence records keep a valid reference.
/// </summary>
public sealed class AbsenceReasonCategory
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>The top-level reason this category belongs to (Informed / Not Informed).</summary>
    public AbsenceKind Kind { get; set; }

    /// <summary>Display name of the category (e.g. "Annual Leave", "No call / no show").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sort order within its kind on the reason drop-down.</summary>
    public int Sequence { get; set; }

    /// <summary>Whether the category is offered for new absences. Inactive categories are hidden
    /// from the drop-down but remain valid on existing records.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp the category was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Optimistic-concurrency token.</summary>
    public byte[]? RowVersion { get; set; }
}
