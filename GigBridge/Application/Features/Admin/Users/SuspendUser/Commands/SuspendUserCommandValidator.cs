using FluentValidation;

namespace Application.Features.Admin.Users.SuspendUser.Commands;

public class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email is invalid");

        RuleFor(command => command.Request.SuspendedUntil)
            .NotEmpty().WithMessage("Suspension end time is required");

        When(command => command.Request.Reason is not null, () =>
        {
            RuleFor(command => command.Request.Reason)
                .MaximumLength(500).WithMessage("Suspension reason must not exceed 500 characters");
        });
    }
}
