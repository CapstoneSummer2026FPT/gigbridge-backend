using FluentValidation;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.Commands;

public class SubmitProposalValidator : AbstractValidator<SubmitProposalCommand>
{
    public SubmitProposalValidator()
    {
        RuleFor(x => x.Request.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.Request.CoverLetter)
            .MaximumLength(4000).WithMessage("CoverLetter must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Request.CoverLetter));

        RuleFor(x => x.Request.ProposedBudget)
            .GreaterThan(0).WithMessage("ProposedBudget must be greater than 0.")
            .When(x => x.Request.ProposedBudget.HasValue);
    }
}
