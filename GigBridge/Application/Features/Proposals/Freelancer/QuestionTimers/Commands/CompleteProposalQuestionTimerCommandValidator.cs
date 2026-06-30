using Domain.Enums;
using FluentValidation;

namespace Application.Features.Proposals.Freelancer.QuestionTimers.Commands;

public class CompleteProposalQuestionTimerCommandValidator
    : AbstractValidator<CompleteProposalQuestionTimerCommand>
{
    public CompleteProposalQuestionTimerCommandValidator()
    {
        RuleFor(x => x.ProposalId)
            .NotEmpty().WithMessage("ProposalId is required.");

        RuleFor(x => x.JobPostQuestionId)
            .NotEmpty().WithMessage("JobPostQuestionId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.AnswerText)
                .MaximumLength(4000).WithMessage("AnswerText must not exceed 4000 characters.");

            RuleFor(x => x.Request.LockedReason)
                .Must(value => Enum.IsDefined(typeof(QuestionTimerLockedReason), value))
                .WithMessage("LockedReason must be Completed or Timeout.");
        });
    }
}
