using FluentValidation;

namespace ManpowerAllocation.Application.Settings;

/// <summary>Validates a shift-settings change: both times must be within a day and distinct.</summary>
public sealed class UpdateShiftSettingsRequestValidator : AbstractValidator<UpdateShiftSettingsRequest>
{
    /// <summary>Configures the validation rules.</summary>
    public UpdateShiftSettingsRequestValidator()
    {
        RuleFor(r => r.DayShiftStart)
            .GreaterThanOrEqualTo(TimeSpan.Zero)
            .LessThan(TimeSpan.FromDays(1))
            .WithMessage("Day shift start must be a time of day between 00:00 and 23:59.");

        RuleFor(r => r.NightShiftStart)
            .GreaterThanOrEqualTo(TimeSpan.Zero)
            .LessThan(TimeSpan.FromDays(1))
            .WithMessage("Night shift start must be a time of day between 00:00 and 23:59.");

        RuleFor(r => r.NightShiftStart)
            .NotEqual(r => r.DayShiftStart)
            .WithMessage("The day and night shift start times must be different.");
    }
}
