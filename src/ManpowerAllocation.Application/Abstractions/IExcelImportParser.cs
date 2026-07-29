using ManpowerAllocation.Application.Models;

namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Parses an uploaded master-data workbook into strongly-typed rows. Implemented in
/// the infrastructure layer with a maintained spreadsheet library (never by hand-rolling
/// a ZIP/XLSX reader as the prototype did). Parsing only produces candidate rows; all
/// validation and persistence happen in the application service.
/// </summary>
public interface IExcelImportParser
{
    /// <summary>
    /// Reads employee attendance rows from the uploaded workbook. The workbook may contain
    /// up to three sheets (EGL, Functional Support, BRG); sheet and column detection mirrors
    /// the original import behaviour.
    /// </summary>
    /// <param name="workbook">The uploaded workbook contents.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The parsed employee rows across all recognised sheets.</returns>
    Task<IReadOnlyList<ImportedEmployeeRow>> ParseAttendanceAsync(Stream workbook, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads department requirement rows (day/night headcount) from the uploaded workbook.
    /// </summary>
    /// <param name="workbook">The uploaded workbook contents.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The parsed requirement rows.</returns>
    Task<IReadOnlyList<ImportedRequirementRow>> ParseRequirementsAsync(Stream workbook, CancellationToken cancellationToken = default);
}
