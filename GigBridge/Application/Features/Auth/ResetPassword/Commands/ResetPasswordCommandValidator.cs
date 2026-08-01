using FluentValidation;

namespace Application.Features.Auth.ResetPassword.Commands;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(command => command.Request.Otp)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches(@"^\d{6}$").WithMessage("Verification code must contain exactly 6 digits.");

        RuleFor(command => command.Request.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Matches(@"^(?=\S{8,}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).*$")
            .WithMessage("Password must contain at least 8 characters, one uppercase letter, one lowercase letter, one number, and one special character.");
    }
}
