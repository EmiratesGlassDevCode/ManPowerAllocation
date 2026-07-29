using ManpowerAllocation.Application.Departments;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>Department maintenance endpoints. Reads require Viewer; edits require Admin.</summary>
public static class DepartmentEndpoints
{
    /// <summary>Maps the department endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapDepartmentEndpoints(this RouteGroupBuilder group)
    {
        var departments = group.MapGroup("/departments");

        departments.MapGet("/", async (Division division, IDepartmentService service, CancellationToken ct) =>
                Results.Ok(await service.GetByDivisionAsync(division, ct)))
            .RequireAuthorization(AuthorizationPolicies.RequireViewer);

        departments.MapPost("/", async (CreateDepartmentRequest request, IDepartmentService service, CancellationToken ct) =>
            {
                var created = await service.CreateAsync(request, ct);
                return Results.Created($"/api/departments/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateDepartmentRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        departments.MapPut("/{id:int}", async (int id, UpdateDepartmentRequest request, IDepartmentService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateDepartmentRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        // Switching a department ON/OFF is a daily operational action, so it is allowed for User and above.
        departments.MapPut("/{id:int}/active", async (int id, SetActiveRequest request, IDepartmentService service, CancellationToken ct) =>
                Results.Ok(await service.SetActiveAsync(id, request.IsActive, ct)))
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        departments.MapDelete("/{id:int}", async (int id, IDepartmentService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
    }

    /// <summary>Request body for toggling a department's active state.</summary>
    /// <param name="IsActive">The desired active state.</param>
    public sealed record SetActiveRequest(bool IsActive);
}
