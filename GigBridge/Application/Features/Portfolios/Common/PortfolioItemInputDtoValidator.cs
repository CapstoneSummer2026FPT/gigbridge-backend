using Application.Features.Portfolios.Common.DTOs;
using FluentValidation;

namespace Application.Features.Portfolios.Common;

public sealed class PortfolioItemInputDtoValidator : AbstractValidator<PortfolioItemInputDto>
{
    public PortfolioItemInputDtoValidator()
    {
        RuleFor(item => item.Title)
            .NotEmpty().WithMessage("Portfolio title is required.")
            .MaximumLength(200).WithMessage("Portfolio title cannot exceed 200 characters.");
        RuleFor(item => item.Description)
            .MaximumLength(2000).WithMessage("Portfolio description cannot exceed 2000 characters.");
        RuleFor(item => item.ProjectUrl)
            .Must(BeValidHttpUrl).WithMessage("Portfolio project URL must be an absolute HTTP or HTTPS URL.")
            .When(item => !string.IsNullOrWhiteSpace(item.ProjectUrl));
        RuleFor(item => item.ImageUrl)
            .Must(BeValidHttpUrl).WithMessage("Portfolio image URL must be an absolute HTTP or HTTPS URL.")
            .When(item => !string.IsNullOrWhiteSpace(item.ImageUrl));
    }

    private static bool BeValidHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
