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
    }
}
