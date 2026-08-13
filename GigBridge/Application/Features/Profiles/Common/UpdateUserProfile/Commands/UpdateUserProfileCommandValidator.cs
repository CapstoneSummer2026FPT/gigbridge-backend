using FluentValidation;
using Application.Features.Contracts.Common.Internal;

namespace Application.Features.Profiles.Common.UpdateUserProfile.Commands;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(command => command.Dto.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name cannot exceed 100 characters.");

        RuleFor(command => command.Dto.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.");

        RuleFor(command => command.Dto.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number cannot exceed 20 characters.")
            .When(command => command.Dto.PhoneNumber is not null);

        RuleFor(command => command.Dto.IdentityOrTaxCode)
            .Must(value => string.IsNullOrWhiteSpace(value) || ContractIdentityCode.IsValid(value))
            .WithMessage("Identity code must contain exactly 9 or 12 digits.");

        RuleFor(command => command.Dto.PreferredLanguage)
            .MaximumLength(5).WithMessage("Preferred language cannot exceed 5 characters.")
            .When(command => command.Dto.PreferredLanguage is not null);
    }
}
