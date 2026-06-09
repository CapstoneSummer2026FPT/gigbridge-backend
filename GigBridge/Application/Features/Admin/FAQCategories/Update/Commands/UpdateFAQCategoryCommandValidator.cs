using FluentValidation;

namespace Application.Features.Admin.FAQCategories.Update.Commands;

public sealed class UpdateFAQCategoryCommandValidator : AbstractValidator<UpdateFAQCategoryCommand>
{
    public UpdateFAQCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Category ID is required");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Name)
                .MaximumLength(200).When(x => x.Request.Name is not null)
                .WithMessage("Category name must not exceed 200 characters");

            RuleFor(x => x.Request.Slug)
                .MaximumLength(200).When(x => x.Request.Slug is not null)
                .WithMessage("Slug must not exceed 200 characters");

        });
    }
}
