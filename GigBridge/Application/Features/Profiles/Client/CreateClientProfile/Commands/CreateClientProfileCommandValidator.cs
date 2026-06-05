using FluentValidation;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.Commands;

namespace Application.Features.Profiles.ClientProfile.CreateClientProfile.Commands;

public class CreateClientProfileCommandValidator : AbstractValidator<CreateClientProfileCommand>
{
    public CreateClientProfileCommandValidator()
    {
        RuleFor(v => v.Dto.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(300).WithMessage("Company name cannot exceed 300 characters.");

        RuleFor(v => v.Dto.Industry)
            .NotEmpty().WithMessage("Industry is required.")
            .MaximumLength(300).WithMessage("Industry cannot exceed 300 characters.");

        RuleFor(v => v.Dto.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(300).WithMessage("Location cannot exceed 300 characters.");

        RuleFor(v => v.Dto.CompanyWebsite)
            .MaximumLength(500).WithMessage("Company website cannot exceed 500 characters.");

        RuleFor(v => v.Dto.CompanyDescription)
            .MaximumLength(2000).WithMessage("Company description cannot exceed 2000 characters.");

        RuleFor(v => v.Dto.CompanySize)
            .InclusiveBetween(0, 3).WithMessage("Invalid company size (0 = Solo, 1 = Small, 2 = Medium, 3 = Large).");
    }
}
