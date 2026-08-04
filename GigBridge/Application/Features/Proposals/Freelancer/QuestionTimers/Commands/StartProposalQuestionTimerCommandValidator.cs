using FluentValidation;

namespace Application.Features.Proposals.Freelancer.QuestionTimers.Commands;

public class StartProposalQuestionTimerCommandValidator
    : AbstractValidator<StartProposalQuestionTimerCommand>
{
    public StartProposalQuestionTimerCommandValidator()
    {
        RuleFor(x => x.ProposalId)
            .NotEmpty().WithMessage("ProposalId is required.");

        RuleFor(x => x.JobPostQuestionId)
            .NotEmpty().WithMessage("JobPostQuestionId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
