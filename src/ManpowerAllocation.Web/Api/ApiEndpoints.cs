namespace ManpowerAllocation.Web.Api;

/// <summary>Aggregates and maps every Minimal API endpoint under the rate-limited "/api" group.</summary>
public static class ApiEndpoints
{
    /// <summary>The name of the rate-limiting policy applied to the whole API surface.</summary>
    public const string RateLimitPolicy = "api";

    /// <summary>Maps all API endpoints.</summary>
    /// <param name="app">The web application to map onto.</param>
    public static void MapApiEndpoints(this WebApplication app)
    {
        // Every endpoint below inherits authentication (the deny-by-default fallback policy),
        // rate limiting, and its own role policy declared in the individual endpoint modules.
        var api = app.MapGroup("/api").RequireRateLimiting(RateLimitPolicy);

        api.MapDashboardEndpoints();
        api.MapDepartmentEndpoints();
        api.MapEmployeeEndpoints();
        api.MapAbsenceEndpoints();
        api.MapAnalyticsEndpoints();
        api.MapExportEndpoints();
        api.MapAdminEndpoints();
    }
}
