using System.Diagnostics;

namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Emits one structured log entry per request capturing the user, method, path, result status
/// and elapsed time. It deliberately logs no request bodies, headers, query values, tokens or
/// PII — only the coarse metadata needed for an access/operations trail.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>Initialises the middleware.</summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="logger">Logger used for the structured entry.</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Times the request and logs a single structured entry when it completes.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // The user id is a non-secret identifier (Entra object id or "breakglass"); no
            // credential or token is ever logged.
            var userId = context.User?.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "authenticated"
                : "anonymous";

            _logger.LogInformation(
                "Request {Method} {Path} by {UserId} responded {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path.Value,
                userId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
