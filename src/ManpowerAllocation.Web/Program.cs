using System.Security.Claims;
using System.Threading.RateLimiting;
using ManpowerAllocation.Application;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Application.Roles;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure;
using ManpowerAllocation.Infrastructure.Persistence;
using ManpowerAllocation.Web.Api;
using ManpowerAllocation.Web.Components;
using ManpowerAllocation.Web.Health;
using ManpowerAllocation.Web.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// ── Application and infrastructure services ─────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Authentication: Entra ID (Microsoft.Identity.Web) is the sole login mechanism ───────
// There is no local username/password system. The only exception is the tightly controlled
// break-glass path, which signs into the same cookie scheme and is fully audited.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Force the secure authorization-code flow. This avoids the implicit "id_token" flow (which
// would require enabling ID tokens in Entra and exposes the token in the browser). Code flow
// keeps tokens off the front channel but requires a valid client secret to redeem the code.
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.ResponseType = "code";

    // The response comes back as a cross-site form_post from login.microsoftonline.com, so the
    // short-lived correlation and nonce cookies MUST be SameSite=None and Secure — otherwise the
    // browser drops them on the return POST and the callback fails to correlate the response.
    options.NonceCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    // Sign-in failures at the callback used to bubble up as an unhandled exception and surface to
    // the user as a raw HTTP 500. The two events below now log the real reason (invalid client
    // secret, missing admin consent, a stale nonce/correlation cookie after a Data Protection key
    // change or app-pool recycle, a replayed callback, reply-URL mismatch, etc.) and then recover
    // gracefully: a first failure re-challenges once with fresh cookies — which transparently fixes
    // the common transient cases — and a repeat failure lands on a friendly page instead of a 500.
    // No tokens or secrets are logged, only the provider error and message.
    options.Events.OnRemoteFailure = context =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Authentication.OpenIdConnect");
        logger.LogError(
            context.Failure,
            "Sign-in failed at the OpenID Connect callback: {Message}",
            context.Failure?.Message);

        RecoverFromSignInFailure(context.HttpContext);
        context.HandleResponse();
        return Task.CompletedTask;
    };

    options.Events.OnAuthenticationFailed = context =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Authentication.OpenIdConnect");
        logger.LogError(
            context.Exception,
            "Sign-in token validation failed: {Message}",
            context.Exception?.Message);

        RecoverFromSignInFailure(context.HttpContext);
        context.HandleResponse();
        return Task.CompletedTask;
    };

    // On a successful sign-in, ensure the user has at least a Viewer role. Access is still gated
    // upstream by the Entra enterprise-app assignment (only assigned users receive a token), so
    // everyone who reaches here is an approved user and is provisioned Viewer on first login.
    // Administrators are promoted to User/Admin explicitly from the Role Assignments screen; an
    // existing assignment is never downgraded.
    var previousTokenValidated = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (previousTokenValidated is not null)
        {
            await previousTokenValidated(context);
        }

        // The sign-in succeeded — clear any auto-retry marker left by an earlier failed attempt.
        ClearSignInRetryCookie(context.HttpContext);

        var objectId = context.Principal?.GetObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return;
        }

        var displayName = context.Principal?.FindFirst("name")?.Value
            ?? context.Principal?.FindFirst("preferred_username")?.Value;
        var services = context.HttpContext.RequestServices;

        try
        {
            var roleService = services.GetRequiredService<IRoleService>();
            await roleService.EnsureDefaultViewerAsync(objectId, displayName, context.HttpContext.RequestAborted);

            // Record the sign-in in the audit trail. Written directly (not via IAuditWriter)
            // because the principal is not yet established as HttpContext.User at this point.
            var db = services.GetRequiredService<IApplicationDbContext>();
            var clock = services.GetRequiredService<IClock>();
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = objectId,
                UserDisplayName = displayName,
                TimestampUtc = clock.UtcNow,
                Action = AuditAction.SignIn,
                EntityName = "Authentication",
                RecordId = null,
                OldValue = null,
                NewValue = null,
                IsBreakGlassSession = false
            });
            await db.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Provisioning the default Viewer role and writing the sign-in audit are best-effort:
            // a transient database hiccup here must NOT fail an otherwise valid sign-in (which
            // would otherwise reach OnAuthenticationFailed and be shown as an error). The role is
            // resolved again on the next request by the claims transformation, so log and let the
            // login proceed.
            var logger = services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication.OpenIdConnect");
            logger.LogError(ex, "Post-sign-in provisioning/audit failed for {ObjectId}; sign-in still allowed.", objectId);
        }
    };
});

// Harden the session cookie: HttpOnly, Secure, SameSite=Lax.
//
// NOTE / deliberate deviation from the spec's "SameSite=Strict": Strict is incompatible with
// interactive Entra ID sign-in. After authenticating, Microsoft returns the user to the app via a
// cross-site navigation; a Strict cookie is NOT sent on that first request, so the app sees no
// session, bounces back to Microsoft ("We couldn't sign you in"), and the login only "works" when
// the site is later opened directly in a new tab (a same-site request). Lax is the correct,
// still-CSRF-safe setting for an interactive auth cookie (it is what Microsoft's own templates
// use): it is withheld from cross-site sub-requests and cross-site POSTs but sent on the top-level
// return navigation from the identity provider. This conflict is flagged to IT; keeping SSO
// working requires Lax here.
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "__Host-ManpowerAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // The __Host- prefix requires Path=/ and no Domain; pin it so the cookie is valid and the
    // prefix rule is satisfied (the app is hosted at the site root).
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/access-denied";

    // Enforce the emergency window on LIVE sessions, not just new logins. An already-issued
    // break-glass cookie must stop working the moment the account is auto-disabled (or its
    // four-hour window elapses); otherwise the cookie would remain valid for its full lifetime.
    // Only break-glass principals trigger the database check, so normal Entra sessions are
    // unaffected.
    options.Events ??= new CookieAuthenticationEvents();
    var previousValidate = options.Events.OnValidatePrincipal;
    options.Events.OnValidatePrincipal = async context =>
    {
        if (previousValidate is not null)
        {
            await previousValidate(context);
        }

        if (context.Principal?.HasClaim(AppClaimTypes.BreakGlass, "true") != true)
        {
            return;
        }

        var breakGlass = context.HttpContext.RequestServices.GetRequiredService<IBreakGlassService>();
        var status = await breakGlass.GetStatusAsync(context.HttpContext.RequestAborted);
        var windowElapsed = status.AutoDisableAtUtc is { } disableAt && DateTime.UtcNow >= disableAt;

        if (!status.IsEnabled || windowElapsed)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ManpowerAllocation.Web.Common.DisplayTimeZone>();

// Resolve the application role from the Role Assignment table on every authentication.
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AppRoleClaimsTransformation>();

// Role-based authorization policies plus a deny-by-default fallback (SSO on every route).
builder.Services.AddAppAuthorization();

// Controllers are required only for the Microsoft.Identity.Web sign-in/sign-out UI.
builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

// ── Rate limiting on the API and the emergency login ────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ApiEndpoints.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // The emergency credential endpoint gets its own far stricter bucket (a handful of attempts
    // per five minutes per client) so the Admin-granting secret cannot be brute-forced.
    options.AddPolicy(BreakGlassAuthEndpoints.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

// Behind IIS the app sees the reverse proxy's address unless the forwarded headers are honoured.
// Processing X-Forwarded-For gives the real client IP for rate-limit partitioning and logging.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    // The only upstream proxy is the local IIS site, so the default localhost restriction is
    // cleared to trust the header it forwards.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Generic error responses (never leak stack traces, SQL or paths) ─────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── HSTS ────────────────────────────────────────────────────────────────────────────────
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// ── Data Protection ─────────────────────────────────────────────────────────────────────
// Persist keys to a stable folder in production so the auth cookie and antiforgery tokens
// survive app-pool recycles under IIS (otherwise every recycle silently signs users out).
// Set "DataProtection:KeyPath" to a folder the app-pool identity can read/write.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("ManpowerAllocation");
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

// ── Health checks ───────────────────────────────────────────────────────────────────────
// A liveness probe (process is up) and a readiness probe (database reachable, biometric feed and
// snapshot worker healthy) for load balancers and monitoring. The readiness checks are tagged so
// the liveness endpoint can exclude them.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<AttendanceSourceHealthCheck>("attendance", tags: new[] { "ready" })
    .AddCheck<SnapshotWorkerHealthCheck>("snapshot-worker", tags: new[] { "ready" });

// ── Blazor Server + Fluent UI ───────────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Warn loudly when Data Protection keys are not being persisted outside Development. Without a
// stable key path the key ring is regenerated on every app-pool recycle, which intermittently
// breaks the sign-in callback (the nonce/correlation cookie can no longer be decrypted) and signs
// users out on recycle. Set "DataProtection:KeyPath" to a folder the app-pool identity can
// read/write — and a shared folder if the app runs on more than one server.
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    app.Logger.LogWarning(
        "DataProtection:KeyPath is not configured. Keys will not persist across app-pool recycles, " +
        "which can intermittently fail the sign-in callback. Set it to a stable folder the app-pool " +
        "identity can read/write (shared across servers if load-balanced).");
}

// ── HTTP pipeline ───────────────────────────────────────────────────────────────────────
// Honour the forwarded client IP/scheme from the IIS reverse proxy before anything reads them.
app.UseForwardedHeaders();

// The exception handler runs first so every failure (in any environment) yields a generic body.
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security response headers on every response.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles();

// One structured, PII-free log line per request.
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Endpoints.
// Health probes are anonymous (load balancers cannot authenticate) and carry no sensitive detail.
// Liveness ignores all checks — it only proves the process answers; readiness runs the tagged checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapControllers();
app.MapApiEndpoints();
app.MapBreakGlassAuthEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// ── Apply migrations and seed the disabled break-glass row at startup ───────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

// Writes a compact, non-sensitive JSON summary of the readiness report. Only the check name, its
// status and the (author-controlled, generic) description are exposed — never exception detail.
static Task WriteHealthResponse(HttpContext httpContext, HealthReport report)
{
    httpContext.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description
        })
    };
    return httpContext.Response.WriteAsJsonAsync(payload);
}

// Name of the short-lived marker cookie that bounds sign-in auto-retries to a single attempt.
const string SignInRetryCookie = "mpa_signin_retry";

// Recovers from a failed OpenID Connect callback without ever returning a raw 500. The first
// failure for a browser re-challenges once by bouncing through the app root (guarded by the
// deny-by-default policy), which mints fresh state/nonce/correlation cookies — transparently
// fixing the common transient causes (a Data Protection key change or app-pool recycle that left
// the previous nonce cookie undecryptable, or a replayed/expired callback). A repeat failure means
// the problem is not transient, so it lands on a friendly, branded page instead of looping.
static void RecoverFromSignInFailure(HttpContext httpContext)
{
    if (httpContext.Request.Cookies.ContainsKey(SignInRetryCookie))
    {
        httpContext.Response.Cookies.Delete(SignInRetryCookie, new CookieOptions { Path = "/" });
        httpContext.Response.Redirect("/signin-error");
        return;
    }

    httpContext.Response.Cookies.Append(SignInRetryCookie, "1", new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromMinutes(5)
    });
    httpContext.Response.Redirect("/");
}

// Clears the auto-retry marker after a successful sign-in so the next genuine failure is retried.
static void ClearSignInRetryCookie(HttpContext httpContext) =>
    httpContext.Response.Cookies.Delete(SignInRetryCookie, new CookieOptions { Path = "/" });

// Rate-limit partition key: the authenticated principal where possible, else the client IP.
static string ResolveRateLimitKey(HttpContext httpContext)
{
    if (httpContext.User?.Identity?.IsAuthenticated == true)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "authenticated";
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}
