using Application.Features.WorkExperiences.Common.DTOs;
using FluentValidation;

namespace Application.Features.WorkExperiences.Common;

public sealed class WorkExperienceInputDtoValidator : AbstractValidator<WorkExperienceInputDto>
{
    public WorkExperienceInputDtoValidator()
    {
        RuleFor(experience => experience.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(300).WithMessage("Company name cannot exceed 300 characters.");

        RuleFor(experience => experience.JobTitle)
            .NotEmpty().WithMessage("Job title is required.")
            .MaximumLength(300).WithMessage("Job title cannot exceed 300 characters.");

        RuleFor(experience => experience.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(experience => experience.EndDate)
            .GreaterThanOrEqualTo(experience => experience.StartDate)
            .WithMessage("End date cannot be before start date.")
            .When(experience => experience.EndDate.HasValue);

        RuleFor(experience => experience.Description)
            .MaximumLength(2000).WithMessage("Work experience description cannot exceed 2000 characters.");
    }
}
