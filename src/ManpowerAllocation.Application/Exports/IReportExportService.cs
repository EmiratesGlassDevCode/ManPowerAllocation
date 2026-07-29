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
}
