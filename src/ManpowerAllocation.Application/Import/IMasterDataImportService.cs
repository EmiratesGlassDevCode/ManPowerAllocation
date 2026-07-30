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

    /// <summary>
    /// Applies edits from a workbook that was downloaded from the app's Attendance export and then
    /// changed. This is a non-destructive round-trip: each row is matched back to an existing
    /// employee (by the Ref column, falling back to a unique badge number) and only that
    /// employee's department, shift, status, outsource flag, name, badge and notes are updated.
    /// Nothing is ever deleted, and rows that match no employee are reported as warnings rather
    /// than inserted (new staff are added through the Add Employee form). The whole apply runs in
    /// one transaction.
    /// </summary>
    /// <param name="workbook">The uploaded, edited workbook stream.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ImportResult> ApplyAttendanceEditsAsync(Stream workbook, CancellationToken cancellationToken = default);
}
