namespace ManpowerAllocation.Application.Import;

/// <summary>
/// Imports master data (attendance and requirements) from an uploaded workbook into the
/// governed database. This is an administrator-only seeding operation; day-to-day changes
/// are made through the normal create/update endpoints, not by re-importing.
/// </summary>
public interface IMasterDataImportService
{
    /// <summary>
    /// Imports employee attendance from an uploaded workbook. Departments referenced by the
    /// file are created if missing. When <paramref name="replaceExisting"/> is false, a
    /// division that already has employees is left untouched and reported as a warning; when
    /// true, that division's employees are replaced. The whole import runs in one transaction.
    /// </summary>
    /// <param name="workbook">The uploaded workbook stream.</param>
    /// <param name="replaceExisting">Whether to replace employees for divisions that already contain data.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ImportResult> ImportAttendanceAsync(Stream workbook, bool replaceExisting, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports department day/night requirements from an uploaded workbook. Existing
    /// departments are updated; missing departments are created. Attendance is never affected.
    /// </summary>
    /// <param name="workbook">The uploaded workbook stream.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ImportResult> ImportRequirementsAsync(Stream workbook, CancellationToken cancellationToken = default);
}
