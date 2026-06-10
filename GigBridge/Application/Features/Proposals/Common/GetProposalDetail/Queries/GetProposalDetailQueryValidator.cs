using FluentValidation;

namespace Application.Features.Proposals.Common.GetProposalDetail.Queries;

public class GetProposalDetailQueryValidator : AbstractValidator<GetProposalDetailQuery>
{
    public GetProposalDetailQueryValidator()
    {
        RuleFor(x => x.ProposalId)
            .NotEmpty()
            .WithMessage("ProposalId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
