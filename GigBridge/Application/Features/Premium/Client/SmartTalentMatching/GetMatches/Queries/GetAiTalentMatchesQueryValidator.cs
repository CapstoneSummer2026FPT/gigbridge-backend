using FluentValidation;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed class GetAiTalentMatchesQueryValidator : AbstractValidator<GetAiTalentMatchesQuery>
{
    public GetAiTalentMatchesQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.JobPostId).NotEmpty();
        RuleFor(query => query.TopK).InclusiveBetween(1, 20);
        RuleFor(x => x.Filters!.Availability).InclusiveBetween(0, 1)
            .When(x => x.Filters?.Availability.HasValue == true);
        RuleFor(x => x.Filters!.MinimumProficiency).InclusiveBetween(0, 3)
            .When(x => x.Filters?.MinimumProficiency.HasValue == true);
        RuleFor(x => x.Filters!.MinimumYears).InclusiveBetween(0, 80)
            .When(x => x.Filters?.MinimumYears.HasValue == true);
        RuleFor(x => x.Filters!.MinimumRating).InclusiveBetween(0, 5)
            .When(x => x.Filters?.MinimumRating.HasValue == true);
        RuleFor(x => x.Filters!.MinimumCompletedContracts).GreaterThanOrEqualTo(0)
            .When(x => x.Filters?.MinimumCompletedContracts.HasValue == true);
        RuleFor(x => x.Filters!.SkillIds!).Must(ids => ids.Distinct().Count() <= 10)
            .When(x => x.Filters?.SkillIds is not null)
            .WithMessage("Select no more than 10 skill filters.");
        RuleForEach(x => x.Filters!.SkillIds!).NotEmpty()
            .When(x => x.Filters?.SkillIds is not null);
    }
}
