using System.Security.Claims;
using ManpowerAllocation.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Defines the role-based authorization policies enforced server-side on every route and
/// endpoint. A policy is satisfied when the principal carries an <see cref="AppClaimTypes.AppRole"/>
/// claim whose rank is at least the required role, so both Entra ID users and an emergency
/// break-glass session are handled uniformly.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires the Viewer role or higher.</summary>
    public const string RequireViewer = "RequireViewer";

    /// <summary>Requires the User role or higher.</summary>
    public const string RequireUser = "RequireUser";

    /// <summary>Requires the Admin role.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Registers the application's authorization policies and a deny-by-default fallback.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequireViewer, policy => policy.RequireAssertion(ctx => HasAtLeast(ctx.User, UserRole.Viewer)));
            options.AddPolicy(RequireUser, policy => policy.RequireAssertion(ctx => HasAtLeast(ctx.User, UserRole.User)));
            options.AddPolicy(RequireAdmin, policy => policy.RequireAssertion(ctx => HasAtLeast(ctx.User, UserRole.Admin)));

            // Deny by default: every endpoint requires an authenticated principal that additionally
            // holds at least the Viewer role. Routes that must be reachable anonymously (the
            // break-glass login and the sign-in callbacks) opt out explicitly with AllowAnonymous.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => HasAtLeast(ctx.User, UserRole.Viewer))
                .Build();
        });

        return services;
    }

    /// <summary>Returns true when the principal holds an application role of at least the required rank.</summary>
    private static bool HasAtLeast(ClaimsPrincipal user, UserRole minimumRole)
    {
        var raw = user.FindFirst(AppClaimTypes.AppRole)?.Value;
        return Enum.TryParse<UserRole>(raw, out var role) && role >= minimumRole;
    }
}
