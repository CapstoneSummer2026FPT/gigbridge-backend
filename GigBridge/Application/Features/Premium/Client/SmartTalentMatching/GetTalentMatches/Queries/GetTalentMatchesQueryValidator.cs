using FluentValidation;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetTalentMatches.Queries;

public sealed class GetTalentMatchesQueryValidator : AbstractValidator<GetTalentMatchesQuery>
{
    public GetTalentMatchesQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.JobPostId).NotEmpty();
        RuleFor(x => x.TopK).InclusiveBetween(1, 50);
    }
}
