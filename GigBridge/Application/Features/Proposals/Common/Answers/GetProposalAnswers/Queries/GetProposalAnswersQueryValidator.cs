using FluentValidation;

namespace Application.Features.Proposals.Common.Answers.GetProposalAnswers.Queries;

public class GetProposalAnswersQueryValidator : AbstractValidator<GetProposalAnswersQuery>
{
    public GetProposalAnswersQueryValidator()
    {
        RuleFor(x => x.ProposalsId)
            .NotEmpty().WithMessage("ProposalsId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.");
    }
}
