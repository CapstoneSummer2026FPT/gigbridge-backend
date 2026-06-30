using FluentValidation;

namespace Application.Features.Proposals.Freelancer.InterviewReview.Commands;

public class StartInterviewReviewCommandValidator
    : AbstractValidator<StartInterviewReviewCommand>
{
    public StartInterviewReviewCommandValidator()
    {
        RuleFor(x => x.ProposalId)
            .NotEmpty().WithMessage("ProposalId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
