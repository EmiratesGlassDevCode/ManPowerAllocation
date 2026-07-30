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
            var file = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
                ? await service.BuildSnapshotHistoryCsvAsync(fromDate, toDate, ct)
                : await service.BuildSnapshotHistoryExcelAsync(fromDate, toDate, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });
    }
}
