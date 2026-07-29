using ManpowerAllocation.Application.Auditing;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Application.Import;
using ManpowerAllocation.Application.Roles;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>Administration endpoints (role assignment, master-data import, break-glass status). Require Admin.</summary>
public static class AdminEndpoints
{
    /// <summary>Maps the admin endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapAdminEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin").RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        MapRoleEndpoints(admin);
        MapImportEndpoints(admin);

        admin.MapGet("/break-glass/status", async (IBreakGlassService service, CancellationToken ct) =>
            Results.Ok(await service.GetStatusAsync(ct)));

        admin.MapGet("/audit", async (int? take, bool? breakGlassOnly, IAuditReadService service, CancellationToken ct) =>
            Results.Ok(await service.GetRecentAsync(take ?? 100, breakGlassOnly ?? false, ct)));
    }

    /// <summary>Maps the role-assignment endpoints.</summary>
    private static void MapRoleEndpoints(RouteGroupBuilder admin)
    {
        var roles = admin.MapGroup("/roles");

        roles.MapGet("/", async (IRoleService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        roles.MapPut("/", async (UpsertRoleRequest request, IRoleService service, CancellationToken ct) =>
                Results.Ok(await service.UpsertAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<UpsertRoleRequest>>();

        roles.MapDelete("/{id:int}", async (int id, IRoleService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            });
    }

    /// <summary>Maps the master-data import endpoints.</summary>
    private static void MapImportEndpoints(RouteGroupBuilder admin)
    {
        var import = admin.MapGroup("/import");

        // Antiforgery is disabled on these multipart endpoints; they are protected by the Admin
        // policy, per-endpoint rate limiting and the SameSite=Strict session cookie.
        import.MapPost("/attendance", async (IFormFile file, bool replaceExisting, IMasterDataImportService service, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await service.ImportAttendanceAsync(stream, replaceExisting, ct));
            })
            .DisableAntiforgery();

        import.MapPost("/requirements", async (IFormFile file, IMasterDataImportService service, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await service.ImportRequirementsAsync(stream, ct));
            })
            .DisableAntiforgery();
    }
}
