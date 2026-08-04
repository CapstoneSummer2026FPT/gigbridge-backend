using FluentValidation;

namespace Application.Features.JobPosts.Client.DeleteEmptyDraftJobPost.Commands;

public sealed class DeleteEmptyDraftJobPostCommandValidator
    : AbstractValidator<DeleteEmptyDraftJobPostCommand>
{
    public DeleteEmptyDraftJobPostCommandValidator()
    {
        RuleFor(x => x.JobPostId)
            .NotEmpty().WithMessage("JobPostId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
