using FluentValidation;
using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Departments;

/// <summary>Server-side validation for <see cref="CreateDepartmentRequest"/>.</summary>
public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Division)
            .IsInEnum().WithMessage("A valid division is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(120).WithMessage("Department name must be 120 characters or fewer.");

        RuleFor(x => x.RequiredDay)
            .InclusiveBetween(0, 100_000).WithMessage("Day requirement must be between 0 and 100000.");

        RuleFor(x => x.RequiredNight)
            .InclusiveBetween(0, 100_000).WithMessage("Night requirement must be between 0 and 100000.");

        RuleFor(x => x.Sequence)
            .InclusiveBetween(0m, 100_000m).WithMessage("Sequence must be between 0 and 100000.");
    }
}

/// <summary>Server-side validation for <see cref="UpdateDepartmentRequest"/>.</summary>
public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.RequiredDay)
            .InclusiveBetween(0, 100_000).WithMessage("Day requirement must be between 0 and 100000.");

        RuleFor(x => x.RequiredNight)
            .InclusiveBetween(0, 100_000).WithMessage("Night requirement must be between 0 and 100000.");

        RuleFor(x => x.Sequence)
            .InclusiveBetween(0m, 100_000m).WithMessage("Sequence must be between 0 and 100000.");
    }
}
