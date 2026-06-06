using FluentValidation;

namespace Application.Features.Auth.ChangePassword.Commands;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(v => v.Request.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(v => v.Request.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Matches(@"^(?=\S{8,}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).*$")
            .WithMessage("Password must contain at least 8 characters, one uppercase letter, one lowercase letter, one number, and one special character.");
    }
}
