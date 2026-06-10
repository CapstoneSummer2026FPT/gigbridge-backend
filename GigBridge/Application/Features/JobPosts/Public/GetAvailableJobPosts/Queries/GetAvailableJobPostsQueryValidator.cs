using FluentValidation;

namespace Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;

public class GetAvailableJobPostsQueryValidator : AbstractValidator<GetAvailableJobPostsQuery>
{
    public GetAvailableJobPostsQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThan(0)
            .WithMessage("PageIndex must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.BudgetType)
            .Must(x => x is null or 0 or 1)
            .WithMessage("BudgetType must be 0=Fixed or 1=Hourly.");

        RuleFor(x => x)
            .Must(x => !x.BudgetMin.HasValue || !x.BudgetMax.HasValue || x.BudgetMin.Value <= x.BudgetMax.Value)
            .WithMessage("BudgetMin must be less than or equal to BudgetMax.");
    }
}
