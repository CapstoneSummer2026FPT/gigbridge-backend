using FluentValidation;

namespace Application.Features.Proposals.Freelancer.InterviewReview.Commands;

public class CompleteInterviewReviewCommandValidator
    : AbstractValidator<CompleteInterviewReviewCommand>
{
    public CompleteInterviewReviewCommandValidator()
    {
        RuleFor(x => x.ProposalId)
            .NotEmpty().WithMessage("ProposalId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
