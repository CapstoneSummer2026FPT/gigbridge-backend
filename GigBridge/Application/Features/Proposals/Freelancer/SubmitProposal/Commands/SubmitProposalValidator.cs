using FluentValidation;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.Commands;

public class SubmitProposalValidator : AbstractValidator<SubmitProposalCommand>
{
    public SubmitProposalValidator()
    {
        RuleFor(x => x.Request.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId is required.");

        RuleFor(x => x.Request.CoverLetter)
            .NotEmpty().WithMessage("CoverLetter is required.")
            .MinimumLength(50).WithMessage("CoverLetter must be at least 50 characters.");

        RuleFor(x => x.Request.ProposedBudget)
            .NotNull().WithMessage("ProposedBudget is required.")
            .GreaterThan(0).WithMessage("ProposedBudget must be greater than 0.");
    }
}
