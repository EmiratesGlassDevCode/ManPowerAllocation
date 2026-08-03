using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Application.Reconciliation;
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
            var file = fmt switch
            {
                "csv" => await service.BuildSnapshotHistoryCsvAsync(fromDate, toDate, ct),
                "pdf" => await service.BuildSnapshotHistoryPdfAsync(fromDate, toDate, ct),
                _ => await service.BuildSnapshotHistoryExcelAsync(fromDate, toDate, ct)
            };
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        // Biometric reconciliation workbook (verification summary + unmatched IDs + no-badge list).
        // Admin-only: it exposes roster gaps and raw biometric identifiers.
        exports.MapGet("/reconciliation", async (IReconciliationService reconciliation, IReportExportService service, CancellationToken ct) =>
        {
            var report = await reconciliation.BuildAsync(ct);
            var file = await service.BuildReconciliationExcelAsync(report, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        // A single captured daily report. ?id=<snapshotId>&format=pdf|xlsx — the PDF is the branded
        // management dashboard; xlsx is a simple single-sheet workbook.
        exports.MapGet("/snapshot", async (IReportExportService service, long id, string? format, CancellationToken ct) =>
        {
            var fmt = format?.ToLowerInvariant();
            var file = fmt == "xlsx" || fmt == "excel"
                ? await service.BuildDailyReportExcelAsync(id, ct)
                : await service.BuildDailyReportPdfAsync(id, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });
    }
}
