using FluentValidation;

namespace Application.Features.Proposals.SubmitProposal.Commands;

public class SubmitProposalValidator : AbstractValidator<SubmitProposalCommand>
{
    public SubmitProposalValidator()
    {
        RuleFor(x => x.Request.JobPostsId)
            .NotEmpty().WithMessage("JobPostsId không được để trống.");

        RuleFor(x => x.Request.CoverLetter)
            .NotEmpty().WithMessage("Cover Letter không được để trống.")
            .MinimumLength(50).WithMessage("Cover Letter phải có ít nhất 50 ký tự.");

        RuleFor(x => x.Request.ProposedRate)
            .GreaterThan(0).When(x => x.Request.ProposedRate.HasValue)
            .WithMessage("Mức giá đề xuất phải lớn hơn 0.");
    }
}