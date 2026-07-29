namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Adds the mandated security response headers to every response. HSTS itself is applied by
/// <c>UseHsts</c>; this middleware sets Content-Security-Policy, X-Frame-Options,
/// X-Content-Type-Options, Referrer-Policy and Permissions-Policy.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    // A strict policy. Blazor Server serves its script from the same origin and connects back
    // over a same-origin WebSocket (covered by connect-src 'self'). 'unsafe-inline' is permitted
    // for styles only, which the Fluent UI web components require; scripts may not be inlined.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    /// <summary>Initialises the middleware.</summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Adds the headers before invoking the rest of the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["X-Frame-Options"] = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";

        await _next(context);
    }
}
