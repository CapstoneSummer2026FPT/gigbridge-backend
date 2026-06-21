using FluentValidation;

namespace Application.Features.SavedFreelancers.Client.UnsaveFreelancer.Commands;

public class UnsaveFreelancerCommandValidator : AbstractValidator<UnsaveFreelancerCommand>
{
    public UnsaveFreelancerCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.FreelancerProfileId)
            .NotEmpty()
            .WithMessage("FreelancerProfileId is required.");
    }
}