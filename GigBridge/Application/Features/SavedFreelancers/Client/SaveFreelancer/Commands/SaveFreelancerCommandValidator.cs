using FluentValidation;

namespace Application.Features.SavedFreelancers.Client.SaveFreelancer.Commands;

public class SaveFreelancerCommandValidator : AbstractValidator<SaveFreelancerCommand>
{
    public SaveFreelancerCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.FreelancerProfileId)
            .NotEmpty()
            .WithMessage("FreelancerProfileId is required.");
    }
}