using FluentValidation;

namespace Application.Features.JobPosts.Client.CreateJobPost.Commands;

public class CreateJobPostValidator : AbstractValidator<CreateJobPostCommand>
{
    public CreateJobPostValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Request.Description)
            .NotEmpty().WithMessage("Description is required.");

        RuleFor(x => x.Request.BudgetMin)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.BudgetMin.HasValue)
            .WithMessage("BudgetMin must be greater than or equal to 0.");

        RuleFor(x => x.Request.BudgetMax)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.BudgetMax.HasValue)
            .WithMessage("BudgetMax must be greater than or equal to 0.");

        RuleFor(x => x.Request.BudgetMax)
            .GreaterThanOrEqualTo(x => x.Request.BudgetMin)
            .When(x => x.Request.BudgetMin.HasValue && x.Request.BudgetMax.HasValue)
            .WithMessage("BudgetMax must be greater than or equal to BudgetMin.");

        RuleFor(x => x.Request.EndDate)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.Request.EndDate.HasValue)
            .WithMessage("EndDate must be in the future.");
    }
}
