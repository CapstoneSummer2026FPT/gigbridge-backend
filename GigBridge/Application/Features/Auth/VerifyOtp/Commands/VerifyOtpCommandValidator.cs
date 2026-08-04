using Application.Features.Auth.Common;
using FluentValidation;

namespace Application.Features.Auth.VerifyOtp.Commands;

public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(command => command.VerifyOtpRequest.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(command => command.VerifyOtpRequest.Otp)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches(@"^\d{6}$").WithMessage("Verification code must contain exactly 6 digits.");

        RuleFor(command => command.VerifyOtpRequest.Purpose)
            .Must(value => OtpPurposeNames.TryParse(value, out _))
            .WithMessage("Purpose must be 'signup' or 'password_reset'.");
    }
}
