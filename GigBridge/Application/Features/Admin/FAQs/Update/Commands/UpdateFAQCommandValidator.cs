using FluentValidation;

namespace Application.Features.Admin.FAQs.Update.Commands;

public sealed class UpdateFAQCommandValidator : AbstractValidator<UpdateFAQCommand>
{
    public UpdateFAQCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("FAQ ID is required");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.FaqCategoryId)
                .GreaterThan(0).When(x => x.Request.FaqCategoryId.HasValue)
                .WithMessage("FAQ category ID must be greater than 0");

            RuleFor(x => x.Request.Question)
                .MaximumLength(500).When(x => x.Request.Question is not null)
                .WithMessage("Question must not exceed 500 characters");

            RuleFor(x => x.Request.QuestionVi)
                .MaximumLength(500).When(x => x.Request.QuestionVi is not null)
                .WithMessage("Vietnamese question must not exceed 500 characters");
        });
    }
}
