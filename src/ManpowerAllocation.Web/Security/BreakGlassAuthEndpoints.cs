using System.Security.Claims;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Endpoints for the emergency break-glass sign-in and shared sign-out. The login endpoint is
/// the ONLY anonymous state-changing route; it exists so administrators can reach the system
/// when Entra ID itself is unreachable. A successful login is signed into the standard cookie
/// scheme but tagged as a break-glass session so every subsequent action is audited distinctly.
/// </summary>
public static class BreakGlassAuthEndpoints
{
    /// <summary>Maps the break-glass sign-in and sign-out endpoints.</summary>
    /// <param name="app">The web application to map onto.</param>
    public static void MapBreakGlassAuthEndpoints(this WebApplication app)
    {
        // Anonymous but rate-limited, so the emergency path is reachable while Entra is down
        // yet still protected against brute-force secret guessing.
        app.MapPost("/auth/break-glass", HandleLoginAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(Api.ApiEndpoints.RateLimitPolicy);

        app.MapPost("/auth/logout", (HttpContext context) =>
            {
                var isBreakGlass = string.Equals(
                    context.User.FindFirstValue(AppClaimTypes.BreakGlass),
                    "true",
                    StringComparison.OrdinalIgnoreCase);

                var properties = new AuthenticationProperties { RedirectUri = "/" };

                // Break-glass sessions are local-only, so clearing the cookie is a complete
                // sign-out. Entra sessions must ALSO end the federated session at the identity
                // provider; otherwise the next request to a protected route is silently
                // re-authenticated by the still-active SSO session and sign-out appears to do
                // nothing. Signing out the OpenID Connect scheme redirects to Entra's
                // end-session endpoint and back to the configured SignedOutCallbackPath.
                return isBreakGlass
                    ? Results.SignOut(properties, new[] { CookieAuthenticationDefaults.AuthenticationScheme })
                    : Results.SignOut(properties, new[]
                        {
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            OpenIdConnectDefaults.AuthenticationScheme
                        });
            });
    }

    /// <summary>Validates the emergency secret and, on success, establishes a tagged break-glass session.</summary>
    private static async Task<IResult> HandleLoginAsync(HttpContext context, IBreakGlassService breakGlass)
    {
        var form = await context.Request.ReadFormAsync();
        var userName = form["userName"].ToString();
        var password = form["password"].ToString();
        var clientDescription = context.Connection.RemoteIpAddress?.ToString();

        var accepted = await breakGlass.TryAuthenticateAsync(userName, password, clientDescription, context.RequestAborted);
        if (!accepted)
        {
            // Deliberately generic: never reveal whether the account is enabled or the secret wrong.
            return Results.LocalRedirect("/break-glass?error=1");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userName),
            new("name", "Break-glass emergency account"),
            // Emergency sessions operate with Admin capability so IT can act during an outage.
            new(AppClaimTypes.AppRole, UserRole.Admin.ToString()),
            new(AppClaimTypes.BreakGlass, "true")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Short-lived cookie; the account itself also auto-disables four hours after enabling.
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        return Results.LocalRedirect("/");
    }
}
