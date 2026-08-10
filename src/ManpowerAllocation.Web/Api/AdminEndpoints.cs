using ManpowerAllocation.Application.Attendance;
using ManpowerAllocation.Application.Auditing;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Application.Email;
using ManpowerAllocation.Application.Import;
using ManpowerAllocation.Application.Roles;
using ManpowerAllocation.Application.Settings;
using ManpowerAllocation.Application.Shifts;
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

        admin.MapPost("/attendance/sync", async (IAttendanceSyncService service, CancellationToken ct) =>
            Results.Ok(await service.SyncAsync("manual-api", ct)));

        admin.MapGet("/attendance/status", (AttendanceSyncStatus status) =>
            Results.Ok(status.LastRun));

        admin.MapGet("/shift-settings", async (IShiftSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)));

        admin.MapPut("/shift-settings", async (UpdateShiftSettingsRequest request, IShiftSettingsService service, CancellationToken ct) =>
                Results.Ok(await service.UpdateAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateShiftSettingsRequest>>();

        MapShiftScheduleEndpoints(admin);
        MapEmailSettingsEndpoints(admin);
        MapDepartmentHeadEndpoints(admin);
    }

    /// <summary>Maps the department-head appointment endpoints. Require Admin.</summary>
    private static void MapDepartmentHeadEndpoints(RouteGroupBuilder admin)
    {
        var heads = admin.MapGroup("/department-heads");

        heads.MapGet("/", async (ManpowerAllocation.Application.DepartmentHeads.IDepartmentHeadService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        heads.MapPut("/", async (ManpowerAllocation.Application.DepartmentHeads.UpsertDepartmentHeadRequest request,
            ManpowerAllocation.Application.DepartmentHeads.IDepartmentHeadService service, CancellationToken ct) =>
        {
            await service.UpsertAsync(request, ct);
            return Results.NoContent();
        });

        heads.MapDelete("/{entraObjectId}", async (string entraObjectId,
            ManpowerAllocation.Application.DepartmentHeads.IDepartmentHeadService service, CancellationToken ct) =>
        {
            await service.RevokeAsync(entraObjectId, ct);
            return Results.NoContent();
        });
    }

    /// <summary>Maps the email/SMTP settings endpoints (read, update, send test). Require Admin.</summary>
    private static void MapEmailSettingsEndpoints(RouteGroupBuilder admin)
    {
        var email = admin.MapGroup("/email-settings");

        email.MapGet("/", async (IEmailSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)));

        email.MapPut("/", async (UpdateEmailSettingsRequest request, IEmailSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(request, ct)));

        email.MapPost("/test", async (IEmailSettingsService service, CancellationToken ct) =>
        {
            await service.SendTestAsync(ct);
            return Results.NoContent();
        });
    }

    /// <summary>Maps the per-department shift-schedule endpoints.</summary>
    private static void MapShiftScheduleEndpoints(RouteGroupBuilder admin)
    {
        var schedules = admin.MapGroup("/shift-schedules");

        schedules.MapGet("/", async (IShiftScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        schedules.MapPost("/", async (CreateShiftScheduleRequest request, IShiftScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(request, ct)));

        schedules.MapPut("/{id:int}", async (int id, UpdateShiftScheduleRequest request, IShiftScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));

        schedules.MapGet("/assignments", async (IShiftScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.GetDepartmentAssignmentsAsync(ct)));

        schedules.MapPut("/assignments/{departmentId:int}", async (int departmentId, AssignScheduleRequest request, IShiftScheduleService service, CancellationToken ct) =>
        {
            await service.AssignAsync(departmentId, request.ShiftScheduleId, ct);
            return Results.NoContent();
        });
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

    /// <summary>Largest workbook the import endpoints accept (20 MB), matching the upload UI's stated limit.</summary>
    private const long MaxWorkbookBytes = 20L * 1024 * 1024;

    /// <summary>Maps the master-data import endpoints.</summary>
    private static void MapImportEndpoints(RouteGroupBuilder admin)
    {
        var import = admin.MapGroup("/import");

        // Antiforgery is disabled on these multipart endpoints; they are protected by the Admin
        // policy, per-endpoint rate limiting and the SameSite=Strict session cookie.
        import.MapPost("/attendance", async (IFormFile? file, [Microsoft.AspNetCore.Mvc.FromForm] bool replaceExisting, IMasterDataImportService service, CancellationToken ct) =>
            {
                var invalid = ValidateWorkbook(file);
                if (invalid is not null)
                {
                    return invalid;
                }

                await using var stream = file!.OpenReadStream();
                return Results.Ok(await service.ImportAttendanceAsync(stream, replaceExisting, ct));
            })
            .DisableAntiforgery();

        import.MapPost("/requirements", async (IFormFile? file, IMasterDataImportService service, CancellationToken ct) =>
            {
                var invalid = ValidateWorkbook(file);
                if (invalid is not null)
                {
                    return invalid;
                }

                await using var stream = file!.OpenReadStream();
                return Results.Ok(await service.ImportRequirementsAsync(stream, ct));
            })
            .DisableAntiforgery();
    }

    /// <summary>
    /// Validates an uploaded workbook: it must be present, non-empty, within the size cap and an
    /// .xlsx file. Returns a 400 result describing the problem, or null when the file is valid.
    /// </summary>
    private static IResult? ValidateWorkbook(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest("No file was uploaded.");
        }

        if (file.Length > MaxWorkbookBytes)
        {
            return Results.BadRequest("The uploaded file is too large.");
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Only .xlsx workbooks are supported.");
        }

        return null;
    }
}
