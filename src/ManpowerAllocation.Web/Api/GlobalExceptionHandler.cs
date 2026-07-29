using ApplicationException = ManpowerAllocation.Application.Common.ApplicationException;
using FluentValidationException = FluentValidation.ValidationException;
using ManpowerAllocation.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ManpowerAllocation.Web.Api;

/// <summary>
/// Central exception handler. Full details are logged server-side, but the response body is
/// always generic — no stack trace, SQL error or internal path is ever returned to the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>Initialises the handler.</summary>
    /// <param name="logger">Logger used to record full failure details server-side.</param>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = Map(exception);

        // Log the full exception server-side; expected business exceptions are logged as warnings.
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled error processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("Request to {Path} rejected: {Title}", httpContext.Request.Path, title);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            // For business exceptions the message is safe to surface; for anything else it stays generic.
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again or contact IT support."
                : exception.Message
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    /// <summary>Maps an exception to a status code and safe title.</summary>
    private static (int StatusCode, string Title) Map(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
        ForbiddenException => (StatusCodes.Status403Forbidden, "You do not have permission to perform this action."),
        BusinessRuleException => (StatusCodes.Status409Conflict, exception.Message),
        FluentValidationException => (StatusCodes.Status400BadRequest, "The request was not valid."),
        ApplicationException => (StatusCodes.Status400BadRequest, exception.Message),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };
}
