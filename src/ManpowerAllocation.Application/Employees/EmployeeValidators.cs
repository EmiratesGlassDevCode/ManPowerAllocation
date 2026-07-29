using FluentValidation;

namespace ManpowerAllocation.Application.Employees;

/// <summary>Server-side validation for <see cref="CreateEmployeeRequest"/>.</summary>
public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Employee name is required.")
            .MaximumLength(200).WithMessage("Employee name must be 200 characters or fewer.");

        RuleFor(x => x.BadgeNumber)
            .MaximumLength(50).WithMessage("Badge number must be 50 characters or fewer.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).WithMessage("A department is required.");

        RuleFor(x => x.Shift)
            .IsInEnum().WithMessage("A valid shift is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid attendance status is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.");
    }
}

/// <summary>Server-side validation for <see cref="UpdateEmployeeRequest"/>.</summary>
public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Employee name is required.")
            .MaximumLength(200).WithMessage("Employee name must be 200 characters or fewer.");

        RuleFor(x => x.BadgeNumber)
            .MaximumLength(50).WithMessage("Badge number must be 50 characters or fewer.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.");
    }
}

/// <summary>Server-side validation for <see cref="ChangeStatusRequest"/>.</summary>
public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public ChangeStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid attendance status is required.");
    }
}

/// <summary>Server-side validation for <see cref="ChangeShiftRequest"/>.</summary>
public sealed class ChangeShiftRequestValidator : AbstractValidator<ChangeShiftRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public ChangeShiftRequestValidator()
    {
        RuleFor(x => x.Shift)
            .IsInEnum().WithMessage("A valid shift is required.");
    }
}

/// <summary>Server-side validation for <see cref="MoveEmployeeRequest"/>.</summary>
public sealed class MoveEmployeeRequestValidator : AbstractValidator<MoveEmployeeRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public MoveEmployeeRequestValidator()
    {
        RuleFor(x => x.TargetDepartmentId)
            .GreaterThan(0).WithMessage("A target department is required.");
    }
}
