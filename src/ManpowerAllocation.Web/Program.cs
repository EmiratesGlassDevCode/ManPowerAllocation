using System.Security.Claims;
using System.Threading.RateLimiting;
using ManpowerAllocation.Application;
using ManpowerAllocation.Application.Abstractions;
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

// Harden the session cookie: HttpOnly, Secure and SameSite=Strict, as mandated.
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "__Host-ManpowerAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/access-denied";
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
