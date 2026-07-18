using FluentValidation;

namespace Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;

public class UpdateFreelancerProfileCommandValidator : AbstractValidator<UpdateFreelancerProfileCommand>
{
    public UpdateFreelancerProfileCommandValidator()
    {
        RuleFor(v => v.Dto.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title cannot exceed 300 characters.");

        RuleFor(v => v.Dto.Bio)
            .NotEmpty().WithMessage("Bio is required.")
            .MaximumLength(2000).WithMessage("Bio cannot exceed 2000 characters.");

        RuleFor(v => v.Dto.Availability)
            .InclusiveBetween(0, 2).WithMessage("Invalid availability status (0 = FullTime, 1 = PartTime, 2 = NotAvailable).");

        RuleFor(v => v.Dto.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(300).WithMessage("Location cannot exceed 300 characters.");

        RuleFor(v => v.Dto.MajorId)
            .NotEmpty().WithMessage("Major is required.");

        RuleFor(v => v.Dto.CategoryIds)
            .NotEmpty().WithMessage("At least one category is required.")
            .Must(categoryIds => categoryIds is not null && categoryIds.Distinct().Count() == categoryIds.Count)
            .WithMessage("Duplicate categories are not allowed.");

        RuleFor(v => v.Dto.SkillIds)
            .Must(skillIds => skillIds is null || skillIds.Distinct().Count() == skillIds.Count)
            .WithMessage("Duplicate skills are not allowed.");
    }
}
