using ManpowerAllocation.Application.Employees;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>Employee and attendance endpoints. Reads require Viewer; edits and delete require User.</summary>
public static class EmployeeEndpoints
{
    /// <summary>Maps the employee endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapEmployeeEndpoints(this RouteGroupBuilder group)
    {
        var employees = group.MapGroup("/employees");

        employees.MapGet("/", async (Division division, IEmployeeService service, CancellationToken ct) =>
                Results.Ok(await service.GetByDivisionAsync(division, ct)))
            .RequireAuthorization(AuthorizationPolicies.RequireViewer);

        employees.MapGet("/search", async (string term, IEmployeeService service, CancellationToken ct) =>
                Results.Ok(await service.SearchAsync(term, ct)))
            .RequireAuthorization(AuthorizationPolicies.RequireViewer);

        employees.MapPost("/", async (CreateEmployeeRequest request, IEmployeeService service, CancellationToken ct) =>
            {
                var created = await service.CreateAsync(request, ct);
                return Results.Created($"/api/employees/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateEmployeeRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        employees.MapPut("/{id:int}", async (int id, UpdateEmployeeRequest request, IEmployeeService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateEmployeeRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        employees.MapPut("/{id:int}/status", async (int id, ChangeStatusRequest request, IEmployeeService service, CancellationToken ct) =>
                Results.Ok(await service.ChangeStatusAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<ChangeStatusRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        employees.MapPut("/{id:int}/shift", async (int id, ChangeShiftRequest request, IEmployeeService service, CancellationToken ct) =>
                Results.Ok(await service.ChangeShiftAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<ChangeShiftRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        employees.MapPut("/{id:int}/move", async (int id, MoveEmployeeRequest request, IEmployeeService service, CancellationToken ct) =>
                Results.Ok(await service.MoveAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<MoveEmployeeRequest>>()
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        employees.MapDelete("/{id:int}", async (int id, IEmployeeService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
    }
}
