using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>Excel export endpoints. Require at least the Viewer role.</summary>
public static class ExportEndpoints
{
    /// <summary>Maps the export endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapExportEndpoints(this RouteGroupBuilder group)
    {
        var exports = group.MapGroup("/exports").RequireAuthorization(AuthorizationPolicies.RequireViewer);

        exports.MapGet("/requirements-template", async (IReportExportService service, CancellationToken ct) =>
        {
            var file = await service.BuildRequirementsTemplateAsync(ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        exports.MapGet("/report", async (IReportExportService service, CancellationToken ct) =>
        {
            var file = await service.BuildStaffingReportAsync(ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        exports.MapGet("/attendance", async (IReportExportService service, CancellationToken ct) =>
        {
            var file = await service.BuildAttendanceAsync(ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        // Archived daily-report history as a flat fact table. ?from=&to= (yyyy-MM-dd) bound the
        // range; ?format=csv returns CSV, otherwise Excel.
        exports.MapGet("/history", async (IReportExportService service, DateTime? from, DateTime? to, string? format, CancellationToken ct) =>
        {
            var toDate = to ?? DateTime.UtcNow.Date;
            var fromDate = from ?? toDate.AddDays(-30);
            var fmt = format?.ToLowerInvariant();
            try
            {
                var file = fmt switch
                {
                    "csv" => await service.BuildSnapshotHistoryCsvAsync(fromDate, toDate, ct),
                    "pdf" => await service.BuildSnapshotHistoryPdfAsync(fromDate, toDate, ct),
                    _ => await service.BuildSnapshotHistoryExcelAsync(fromDate, toDate, ct)
                };
                return Results.File(file.Content, file.ContentType, file.FileName);
            }
            catch (Exception ex) when (fmt == "pdf")
            {
                // TEMPORARY DIAGNOSTIC: the global handler masks all failures as a generic 500,
                // which hid the real PDF-generation cause during troubleshooting. Surface the full
                // detail for the PDF path only, so the exact error is visible in the browser.
                // Remove this catch once the PDF export is confirmed working in production.
                return Results.Text(
                    "PDF generation failed. Full diagnostic detail follows:\n\n" + ex,
                    "text/plain; charset=utf-8");
            }
        });
    }
}
