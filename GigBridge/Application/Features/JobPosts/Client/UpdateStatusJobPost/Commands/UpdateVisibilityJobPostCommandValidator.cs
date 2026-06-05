using FluentValidation;

namespace Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;

public class UpdateVisibilityJobPostCommandValidator
    : AbstractValidator<UpdateVisibilityJobPostCommand>
{
    public UpdateVisibilityJobPostCommandValidator()
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

        RuleFor(x => x.Request.Visibility)
            .Must(visibility => visibility == 0 || visibility == 1 || visibility == 2)
            .WithMessage("Visibility must be 0=Public, 1=Private, or 2=InviteOnly.");
    }
}