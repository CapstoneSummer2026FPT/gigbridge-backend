using FluentValidation;

namespace Application.Features.Auth.Login.Commands;

public class LoginWithRefreshCommandValidator : AbstractValidator<LoginWithRefreshCommand>
{
    public LoginWithRefreshCommandValidator()
    {
        RuleFor(v => v.LoginRequest.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(v => v.LoginRequest.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
