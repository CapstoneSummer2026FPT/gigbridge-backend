using FluentValidation;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.Commands;

public class SubmitProposalValidator : AbstractValidator<SubmitProposalCommand>
{
    public SubmitProposalValidator()
    {
        RuleFor(x => x.Request.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId không du?c d? tr?ng.");

        RuleFor(x => x.Request.CoverLetter)
            .NotEmpty().WithMessage("Cover Letter không du?c d? tr?ng.")
            .MinimumLength(50).WithMessage("Cover Letter ph?i có ít nh?t 50 ký t?.");

        RuleFor(x => x.Request.ProposedRate)
            .GreaterThan(0).When(x => x.Request.ProposedRate.HasValue)
            .WithMessage("M?c giá d? xu?t ph?i l?n hon 0.");
    }
}