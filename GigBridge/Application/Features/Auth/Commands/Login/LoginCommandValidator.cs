using FluentValidation;
namespace Application.Features.Auth.Commands.Login;
public class LoginCommandValidator : AbstractValidator<LoginCommand> {
    public LoginCommandValidator() {
        RuleFor(v => v.Username).NotEmpty().WithMessage("User name is required.");
        RuleFor(v => v.Password).NotEmpty().WithMessage("Password is required.");
    }
}