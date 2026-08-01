using FluentValidation;

namespace Application.Features.Auth.ForgotPassword.Commands;

public sealed class SendEmailPasswordChangingCommandValidator
    : AbstractValidator<SendEmailPasswordChangingCommand>
{
    public SendEmailPasswordChangingCommandValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");
    }
}
