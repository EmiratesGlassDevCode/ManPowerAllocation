using System.Security.Claims;
using System.Threading.RateLimiting;
using ManpowerAllocation.Application;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.BreakGlass;
using ManpowerAllocation.Infrastructure;
using ManpowerAllocation.Infrastructure.Persistence;
using ManpowerAllocation.Web.Api;
using ManpowerAllocation.Web.Components;
using ManpowerAllocation.Web.Security;
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

    // The identity library shows a generic "We couldn't sign you in" page and swallows the
    // underlying reason. Chain onto the existing handlers to log the real failure (invalid
    // client secret, missing admin consent, correlation failure after a Data Protection key
    // change, reply-URL mismatch, etc.) so operators can diagnose it. No tokens or secrets are
    // logged — only the provider error and message.
    var previousRemoteFailure = options.Events.OnRemoteFailure;
    options.Events.OnRemoteFailure = async context =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Authentication.OpenIdConnect");
        logger.LogError(
            context.Failure,
            "Sign-in failed at the OpenID Connect callback: {Message}",
            context.Failure?.Message);

        if (previousRemoteFailure is not null)
        {
            await previousRemoteFailure(context);
        }
    };

    var previousAuthFailed = options.Events.OnAuthenticationFailed;
    options.Events.OnAuthenticationFailed = async context =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Authentication.OpenIdConnect");
        logger.LogError(
            context.Exception,
            "Sign-in token validation failed: {Message}",
            context.Exception?.Message);

        if (previousAuthFailed is not null)
        {
            await previousAuthFailed(context);
        }
    };
});

// Harden the session cookie: HttpOnly, Secure and SameSite=Strict, as mandated.
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "__Host-ManpowerAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
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
builder.Services.Configure<Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions>(options =>
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

// ── Blazor Server + Fluent UI ───────────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

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
