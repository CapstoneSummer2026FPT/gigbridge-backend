using FluentValidation;

namespace Application.Features.SavedJobs.Freelancer.SaveJob.Commands;

public class SaveJobCommandValidator : AbstractValidator<SaveJobCommand>
{
    public SaveJobCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");
    }
}