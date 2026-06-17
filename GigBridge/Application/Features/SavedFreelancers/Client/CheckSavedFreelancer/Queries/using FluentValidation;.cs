using FluentValidation;

namespace Application.Features.SavedFreelancers.Client.CheckSavedFreelancer.Queries;

public class CheckSavedFreelancerQueryValidator : AbstractValidator<CheckSavedFreelancerQuery>
{
    public CheckSavedFreelancerQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.FreelancerProfileId)
            .NotEmpty()
            .WithMessage("FreelancerProfileId is required.");
    }
}