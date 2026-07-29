using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>Read-only dashboard endpoints. Require at least the Viewer role.</summary>
public static class DashboardEndpoints
{
    /// <summary>Maps the dashboard endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        var dashboard = group.MapGroup("/dashboard").RequireAuthorization(AuthorizationPolicies.RequireViewer);

        dashboard.MapGet("/summary", async (ShiftFilter shift, IDashboardService service, CancellationToken ct) =>
                Results.Ok(await service.GetFactorySummaryAsync(shift, ct)))
            .WithName("GetFactorySummary");

        dashboard.MapGet("/division/{division}", async (Division division, ShiftFilter shift, IDashboardService service, CancellationToken ct) =>
                Results.Ok(await service.GetDivisionDashboardAsync(division, shift, ct)))
            .WithName("GetDivisionDashboard");
    }
}
