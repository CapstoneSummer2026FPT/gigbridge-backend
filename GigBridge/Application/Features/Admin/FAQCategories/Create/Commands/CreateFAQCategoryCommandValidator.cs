using FluentValidation;

namespace Application.Features.Admin.FAQCategories.Create.Commands;

public sealed class CreateFAQCategoryCommandValidator : AbstractValidator<CreateFAQCategoryCommand>
{
    public CreateFAQCategoryCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("Category name is required")
                .MaximumLength(200).WithMessage("Category name must not exceed 200 characters");

            RuleFor(x => x.Request.Slug)
                .NotEmpty().WithMessage("Slug is required")
                .MaximumLength(200).WithMessage("Slug must not exceed 200 characters");


        });
    }
}
