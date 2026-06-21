using FluentValidation;

namespace Application.Features.Proposals.Freelancer.Answers.UpdateProposalAnswer.Commands;

public class UpdateProposalAnswerCommandValidator
    : AbstractValidator<UpdateProposalAnswerCommand>
{
    public UpdateProposalAnswerCommandValidator()
    {
        RuleFor(x => x.ProposalsId)
            .NotEmpty().WithMessage("ProposalsId is required.");

        RuleFor(x => x.ProposalAnswersId)
            .NotEmpty().WithMessage("ProposalAnswersId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.AnswerText)
                .MaximumLength(4000).WithMessage("AnswerText must not exceed 4000 characters.");
        });
    }
}
