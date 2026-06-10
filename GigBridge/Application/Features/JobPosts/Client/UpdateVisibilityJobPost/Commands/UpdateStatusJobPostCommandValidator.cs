using FluentValidation;

namespace Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;

public class UpdateStatusJobPostCommandValidator
    : AbstractValidator<UpdateStatusJobPostCommand>
{
    public UpdateStatusJobPostCommandValidator()
    {
        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request body is required.");

        RuleFor(x => x.Request.Status) 
            .Must(status => status == 0 || status == 1 || status == 2 || status == 3)
            .WithMessage("Status must be 0=Draft, 1=Open, 2=Closed, or 3=Cancelled.");
    }
}