using ManpowerAllocation.Application.Exports;

namespace ManpowerAllocation.Application.MasterHistory;

/// <summary>Builds downloadable Excel/PDF files for a captured master snapshot.</summary>
public interface IMasterHistoryExportService
{
    /// <summary>Builds the master snapshot as Excel ("xlsx") or PDF ("pdf").</summary>
    /// <param name="snapshotId">The captured snapshot id.</param>
    /// <param name="format">"xlsx"/"excel" for a workbook, otherwise a PDF.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> ExportAsync(long snapshotId, string? format, CancellationToken cancellationToken = default);
}
