using FluentValidation;

namespace Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.Commands;

public class CreateProposalAnswerCommandValidator
    : AbstractValidator<CreateProposalAnswerCommand>
{
    public CreateProposalAnswerCommandValidator()
    {
        RuleFor(x => x.ProposalsId)
            .NotEmpty().WithMessage("ProposalsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.JobPostQuestionId)
                .NotEmpty().WithMessage("JobPostQuestionId is required.");

            RuleFor(x => x.Request.AnswerText)
                .MaximumLength(4000).WithMessage("AnswerText must not exceed 4000 characters.");
        });
    }
}
