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

    /// <summary>
    /// Builds a branded, laid-out PDF of the archived daily reports in the inclusive range —
    /// Emirates Glass header, summary tiles, a fill-vs-required chart and a captured-reports table.
    /// </summary>
    /// <param name="fromDate">Inclusive start date.</param>
    /// <param name="toDate">Inclusive end date.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildSnapshotHistoryPdfAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a branded, dashboard-style management PDF for a single captured daily report:
    /// Emirates Glass banner, KPI tiles, a workforce-composition donut, a per-division fill chart
    /// and a colour-coded department table.
    /// </summary>
    /// <param name="snapshotId">The captured snapshot id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildDailyReportPdfAsync(long snapshotId, CancellationToken cancellationToken = default);

    /// <summary>Builds a simple, single-sheet Excel workbook for one captured daily report.</summary>
    /// <param name="snapshotId">The captured snapshot id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildDailyReportExcelAsync(long snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a two-sheet Excel workbook of the biometric reconciliation: unmatched biometric
    /// identifiers (punches with no employee) and employees with no badge (unmatchable), preceded by
    /// a verification summary of the pulled-vs-roster counts.
    /// </summary>
    /// <param name="report">The reconciliation report to render.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> BuildReconciliationExcelAsync(
        ManpowerAllocation.Application.Reconciliation.ReconciliationReport report,
        CancellationToken cancellationToken = default);
}
