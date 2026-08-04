using FluentValidation;

namespace Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;

public sealed class SearchAvailableJobPostsCommandValidator : AbstractValidator<SearchAvailableJobPostsCommand>
{
    public SearchAvailableJobPostsCommandValidator()
    {
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(120);
        RuleFor(x => x.Category).MaximumLength(120);
        RuleFor(x => x.Skills).MaximumLength(600);
        RuleFor(x => x.WorkType).MaximumLength(40);
        RuleFor(x => x.PostedWithinDays).InclusiveBetween(1, 3650).When(x => x.PostedWithinDays.HasValue);
        RuleFor(x => x.BudgetMin).GreaterThanOrEqualTo(0).When(x => x.BudgetMin.HasValue);
        RuleFor(x => x.BudgetMax).GreaterThanOrEqualTo(0).When(x => x.BudgetMax.HasValue);
        RuleFor(x => x.BudgetMax)
            .GreaterThanOrEqualTo(x => x.BudgetMin!.Value)
            .When(x => x.BudgetMin.HasValue && x.BudgetMax.HasValue);
        RuleFor(x => x.SkillIds).Must(ids => ids is null || ids.Count <= 20)
            .WithMessage("At most 20 skill filters may be supplied.");
        RuleFor(x => x.ActorIdentity).MaximumLength(80);
    }
}
