using FluentValidation;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;

namespace Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;

public class CreateFreelancerProfileCommandValidator : AbstractValidator<CreateFreelancerProfileCommand>
{
    public CreateFreelancerProfileCommandValidator()
    {
        RuleFor(v => v.Dto.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title cannot exceed 300 characters.");

        RuleFor(v => v.Dto.Bio)
            .NotEmpty().WithMessage("Bio is required.")
            .MaximumLength(2000).WithMessage("Bio cannot exceed 2000 characters.");

        RuleFor(v => v.Dto.ExperienceLevel)
            .InclusiveBetween(0, 2).WithMessage("Invalid experience level (0 = Entry, 1 = Intermediate, 2 = Expert).");

        RuleFor(v => v.Dto.Availability)
            .InclusiveBetween(0, 2).WithMessage("Invalid availability status (0 = FullTime, 1 = PartTime, 2 = NotAvailable).");

        RuleFor(v => v.Dto.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(300).WithMessage("Location cannot exceed 300 characters.");
    }
}
