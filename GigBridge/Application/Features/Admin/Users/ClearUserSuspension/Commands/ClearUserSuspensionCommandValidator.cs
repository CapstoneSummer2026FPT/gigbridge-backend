using FluentValidation;

namespace Application.Features.Admin.Users.ClearUserSuspension.Commands;

public class ClearUserSuspensionCommandValidator : AbstractValidator<ClearUserSuspensionCommand>
{
    public ClearUserSuspensionCommandValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email is invalid");
    }
}
