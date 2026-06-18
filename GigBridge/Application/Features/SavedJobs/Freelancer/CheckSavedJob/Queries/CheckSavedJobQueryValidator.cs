using FluentValidation;

namespace Application.Features.SavedJobs.Freelancer.CheckSavedJob.Queries;

public class CheckSavedJobQueryValidator : AbstractValidator<CheckSavedJobQuery>
{
    public CheckSavedJobQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");
    }
}