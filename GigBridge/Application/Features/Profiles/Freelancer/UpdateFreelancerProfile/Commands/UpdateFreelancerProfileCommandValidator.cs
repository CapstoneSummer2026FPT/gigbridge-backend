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

        RuleFor(v => v.Dto.PortfolioItems)
            .Must(items => items is null || items.Count <= 20)
            .WithMessage("A profile cannot contain more than 20 portfolio items.")
            .Must(items => items is null ||
                items.Where(item => item.PortfolioItemId.HasValue)
                    .Select(item => item.PortfolioItemId!.Value)
                    .Distinct()
                    .Count() == items.Count(item => item.PortfolioItemId.HasValue))
            .WithMessage("Duplicate portfolio item IDs are not allowed.");

        RuleForEach(v => v.Dto.PortfolioItems)
            .ChildRules(portfolioItem =>
            {
                portfolioItem.RuleFor(item => item.Title)
                    .NotEmpty().WithMessage("Portfolio title is required.")
                    .MaximumLength(200).WithMessage("Portfolio title cannot exceed 200 characters.");
                portfolioItem.RuleFor(item => item.Description)
                    .MaximumLength(2000).WithMessage("Portfolio description cannot exceed 2000 characters.");
                portfolioItem.RuleFor(item => item.ProjectUrl)
                    .MaximumLength(2048).WithMessage("Portfolio project URL cannot exceed 2048 characters.")
                    .Must(BeValidHttpUrl).WithMessage("Portfolio project URL must be an absolute HTTP or HTTPS URL.")
                    .When(item => !string.IsNullOrWhiteSpace(item.ProjectUrl));
                portfolioItem.RuleFor(item => item.ImageUrl)
                    .MaximumLength(2048).WithMessage("Portfolio image URL cannot exceed 2048 characters.")
                    .Must(BeValidHttpUrl).WithMessage("Portfolio image URL must be an absolute HTTP or HTTPS URL.")
                    .When(item => !string.IsNullOrWhiteSpace(item.ImageUrl));
            });
    }

    private static bool BeValidHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
