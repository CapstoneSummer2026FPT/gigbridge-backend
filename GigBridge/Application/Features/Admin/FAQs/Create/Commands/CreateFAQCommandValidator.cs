using FluentValidation;

namespace Application.Features.Admin.FAQs.Create.Commands;

public sealed class CreateFAQCommandValidator : AbstractValidator<CreateFAQCommand>
{
    public CreateFAQCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.FaqCategoryId)
                .GreaterThan(0).WithMessage("FAQ category ID is required");

            RuleFor(x => x.Request.Question)
                .NotEmpty().WithMessage("Question is required")
                .MaximumLength(500).WithMessage("Question must not exceed 500 characters");

            RuleFor(x => x.Request.Answer)
                .NotEmpty().WithMessage("Answer is required");

            RuleFor(x => x.Request.QuestionVi)
                .MaximumLength(500).When(x => x.Request.QuestionVi is not null)
                .WithMessage("Vietnamese question must not exceed 500 characters");
        });
    }
}
