namespace ManpowerAllocation.Application.Exports;

/// <summary>Generates the downloadable Excel reports (mirrors the prototype's export buttons).</summary>
public interface IReportExportService
{
    /// <summary>Builds the editable master-requirements template (Category / Department / Day / Night / Total / Sequence).</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildRequirementsTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds the per-department staffing report across all divisions (present vs required, absent, vacation, status).</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildStaffingReportAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds the full attendance list (one row per employee) across all divisions.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildAttendanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a flat fact table of the archived daily reports in the inclusive date range — one row
    /// per department per captured shift per day — as an Excel workbook, ready for pivot/BI analysis.
    /// </summary>
    /// <param name="fromDate">Inclusive start date.</param>
    /// <param name="toDate">Inclusive end date.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildSnapshotHistoryExcelAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>Builds the same archived-report fact table as a CSV file.</summary>
    /// <param name="fromDate">Inclusive start date.</param>
    /// <param name="toDate">Inclusive end date.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildSnapshotHistoryCsvAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
