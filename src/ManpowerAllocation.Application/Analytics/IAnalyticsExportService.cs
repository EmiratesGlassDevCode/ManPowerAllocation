using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Analytics;

/// <summary>The analytics report to export.</summary>
public enum AnalyticsReport
{
    /// <summary>Company-wide staffing trend.</summary>
    Overall = 1,

    /// <summary>Per-department staffing performance.</summary>
    Departments = 2,

    /// <summary>Absence breakdowns by reason and department.</summary>
    Absentees = 3,

    /// <summary>One employee's attendance and absence history.</summary>
    Employee = 4
}

/// <summary>The file format for an analytics export.</summary>
public enum AnalyticsExportFormat
{
    /// <summary>ClosedXML workbook (.xlsx).</summary>
    Excel = 1,

    /// <summary>Branded PdfSharp document (.pdf).</summary>
    Pdf = 2
}

/// <summary>Builds downloadable Excel/PDF files for the analytics reports.</summary>
public interface IAnalyticsExportService
{
    /// <summary>Builds the requested report in the requested format for the given filters.</summary>
    /// <param name="report">Which analytics report to render.</param>
    /// <param name="format">Excel or PDF.</param>
    /// <param name="from">Inclusive start date.</param>
    /// <param name="to">Inclusive end date.</param>
    /// <param name="division">Optional division filter (department/absentee reports).</param>
    /// <param name="employeeId">Employee id (required for the employee report).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task<ExportFile> ExportAsync(
        AnalyticsReport report,
        AnalyticsExportFormat format,
        DateTime from,
        DateTime to,
        Division? division,
        int? employeeId,
        CancellationToken cancellationToken = default);
}
