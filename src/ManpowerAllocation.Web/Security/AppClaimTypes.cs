namespace ManpowerAllocation.Web.Security;

/// <summary>Custom claim types used by the application's authorization model.</summary>
public static class AppClaimTypes
{
    /// <summary>Claim carrying the resolved application role name (Viewer / User / Admin).</summary>
    public const string AppRole = "app_role";

    /// <summary>Claim present and set to "true" only for an emergency break-glass session.</summary>
    public const string BreakGlass = "break_glass";
}
