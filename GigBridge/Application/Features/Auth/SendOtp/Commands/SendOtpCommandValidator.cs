using Application.Common.InternalServices.Auth.Services;
using FluentValidation;

namespace Application.Features.Auth.SendOtp.Commands;

public sealed class SendOtpCommandValidator : AbstractValidator<SendOtpCommand>
{
    public SendOtpCommandValidator()
    {
        RuleFor(command => command.SendOtpRequest.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(command => command.SendOtpRequest.Purpose)
            .Must(value =>
                OtpPurposeNames.TryParse(value, out var purpose)
                && purpose is OtpPurpose.Signup or OtpPurpose.IdentityVerification)
            .WithMessage("Purpose must be 'signup' or 'identity_verification'.");
    }
}
