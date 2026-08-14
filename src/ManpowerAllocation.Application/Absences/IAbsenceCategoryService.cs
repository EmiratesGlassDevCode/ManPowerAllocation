namespace ManpowerAllocation.Application.Absences;

/// <summary>
/// Manages the admin-configurable absence sub-categories. Reads are available to any viewer (they
/// populate the reason drop-down); all mutations require the Admin role, enforced server-side.
/// </summary>
public interface IAbsenceCategoryService
{
    /// <summary>Lists categories, optionally including deactivated ones (for the admin screen).</summary>
    /// <param name="includeInactive">When true, inactive categories are returned as well.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<IReadOnlyList<AbsenceCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>Creates a new category under a fixed kind. Admin only.</summary>
    Task<AbsenceCategoryDto> CreateAsync(CreateAbsenceCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renames / re-orders / (de)activates a category. Admin only.</summary>
    Task<AbsenceCategoryDto> UpdateAsync(int id, UpdateAbsenceCategoryRequest request, CancellationToken cancellationToken = default);
}
