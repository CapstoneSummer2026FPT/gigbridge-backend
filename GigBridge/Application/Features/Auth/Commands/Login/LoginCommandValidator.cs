using FluentValidation;
namespace Application.Features.Auth.Commands.Login;
public class LoginCommandValidator : AbstractValidator<LoginCommand> {
    public LoginCommandValidator() {
        RuleFor(v => v.Username).NotEmpty().EmailAddress().WithMessage("Username must be a valid email.");
        RuleFor(v => v.Password).NotEmpty().WithMessage("Password is required.");
    }
}