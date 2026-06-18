using FluentValidation;

namespace Application.Features.SavedJobs.Freelancer.GetMySavedJobs.Queries;

public class GetMySavedJobsQueryValidator : AbstractValidator<GetMySavedJobsQuery>
{
    public GetMySavedJobsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.PageIndex)
            .GreaterThan(0)
            .WithMessage("PageIndex must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}