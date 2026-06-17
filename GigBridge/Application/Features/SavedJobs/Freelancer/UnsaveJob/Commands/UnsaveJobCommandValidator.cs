using FluentValidation;

namespace Application.Features.SavedJobs.Freelancer.UnsaveJob.Commands;

public class UnsaveJobCommandValidator : AbstractValidator<UnsaveJobCommand>
{
    public UnsaveJobCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");
    }
}