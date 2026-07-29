using FluentValidation;

namespace ManpowerAllocation.Application.Roles;

/// <summary>Server-side validation for <see cref="UpsertRoleRequest"/>.</summary>
public sealed class UpsertRoleRequestValidator : AbstractValidator<UpsertRoleRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public UpsertRoleRequestValidator()
    {
        // The Entra object id is a GUID; validating the shape prevents malformed
        // assignments that could never match a real principal.
        RuleFor(x => x.EntraObjectId)
            .NotEmpty().WithMessage("An Entra object id is required.")
            .Must(BeAGuid).WithMessage("The Entra object id must be a valid GUID.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(256).WithMessage("Display name must be 256 characters or fewer.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid role is required.");
    }

    /// <summary>Returns true when the supplied value parses as a GUID.</summary>
    private static bool BeAGuid(string value) => Guid.TryParse(value, out _);
}
