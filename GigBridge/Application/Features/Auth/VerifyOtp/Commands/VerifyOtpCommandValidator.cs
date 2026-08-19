using Application.Common.InternalServices.Auth.Services;
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
            .WithMessage("Purpose must be 'signup', 'password_reset', or 'identity_verification'.");

        RuleFor(command => command.VerifyOtpRequest.IdentityOrTaxCode)
            .Must(value => value is not null && System.Text.RegularExpressions.Regex.IsMatch(
                string.Concat(value.Where(character => !char.IsWhiteSpace(character))),
                @"^([0-9]{9}|[0-9]{12})$"))
            .WithMessage("Identity code must contain exactly 9 or 12 digits.")
            .When(command => string.Equals(
                command.VerifyOtpRequest.Purpose,
                OtpPurposeNames.IdentityVerification,
                StringComparison.OrdinalIgnoreCase));
    }
}
