using FluentValidation;

namespace ManpowerAllocation.Web.Api;

/// <summary>
/// Endpoint filter that runs the FluentValidation validator for the request body before the
/// handler executes, so no state-changing endpoint can be reached with invalid input. Client
/// validation is never trusted; this is the authoritative server-side check.
/// </summary>
/// <typeparam name="T">The request model type to validate.</typeparam>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return Results.BadRequest("A request body is required.");
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is not null)
        {
            var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return Results.ValidationProblem(result.ToDictionary());
            }
        }

        return await next(context);
    }
}
