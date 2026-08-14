using ManpowerAllocation.Application.Absences;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>
/// Endpoints backing the Absentees page: read the current absentees, list reason categories, and
/// record/clear a reason. Reads require Viewer; writes require Viewer at the transport layer but the
/// service additionally enforces that only a User/Admin or an in-scope department head may write.
/// </summary>
public static class AbsenceEndpoints
{
    /// <summary>Maps the absence endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapAbsenceEndpoints(this RouteGroupBuilder group)
    {
        var absences = group.MapGroup("/absences").RequireAuthorization(AuthorizationPolicies.RequireViewer);

        absences.MapGet("/", async (Division? division, IAbsenceService service, CancellationToken ct) =>
            Results.Ok(await service.GetAbsenteesAsync(division, ct)));

        absences.MapGet("/categories", async (IAbsenceCategoryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(includeInactive: false, ct)));

        absences.MapPost("/reason", async (SetAbsenceReasonRequest request, IAbsenceService service, CancellationToken ct) =>
        {
            await service.SetReasonAsync(request, ct);
            return Results.NoContent();
        });

        absences.MapDelete("/reason/{employeeId:int}", async (int employeeId, IAbsenceService service, CancellationToken ct) =>
        {
            await service.ClearReasonAsync(employeeId, ct);
            return Results.NoContent();
        });
    }
}
