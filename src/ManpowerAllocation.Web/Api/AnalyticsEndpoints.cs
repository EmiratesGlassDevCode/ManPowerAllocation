using ManpowerAllocation.Application.Analytics;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Web.Security;

namespace ManpowerAllocation.Web.Api;

/// <summary>
/// Read-only analytics endpoints (overall / department / absentee / employee) over the captured
/// history, all bounded by a From/To date range. Require the Viewer role.
/// </summary>
public static class AnalyticsEndpoints
{
    /// <summary>Maps the analytics endpoints onto the supplied route group.</summary>
    /// <param name="group">The API route group.</param>
    public static void MapAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        var analytics = group.MapGroup("/analytics").RequireAuthorization(AuthorizationPolicies.RequireViewer);

        analytics.MapGet("/overall", async (DateTime from, DateTime to, IAnalyticsService service, CancellationToken ct) =>
            Results.Ok(await service.GetOverallTrendAsync(from, to, ct)));

        analytics.MapGet("/departments", async (DateTime from, DateTime to, Division? division, IAnalyticsService service, CancellationToken ct) =>
            Results.Ok(await service.GetDepartmentTrendAsync(from, to, division, ct)));

        analytics.MapGet("/absentees", async (DateTime from, DateTime to, Division? division, IAnalyticsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAbsenteeAnalyticsAsync(from, to, division, ct)));

        analytics.MapGet("/employee/{employeeId:int}", async (int employeeId, DateTime from, DateTime to, IAnalyticsService service, CancellationToken ct) =>
        {
            var result = await service.GetEmployeeHistoryAsync(employeeId, from, to, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        analytics.MapGet("/employees", async (IAnalyticsService service, CancellationToken ct) =>
            Results.Ok(await service.GetEmployeeOptionsAsync(ct)));
    }
}
