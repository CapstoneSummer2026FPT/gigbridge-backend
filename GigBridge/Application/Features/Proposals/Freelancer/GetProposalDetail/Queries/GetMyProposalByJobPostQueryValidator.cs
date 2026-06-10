using Application.Features.Proposals.Freelancer.GetMyProposalByJobPost.Queries;
using FluentValidation;

namespace Application.Features.Proposals.Freelancer.GetProposalDetail.Queries;

public class GetMyProposalByJobPostQueryValidator : AbstractValidator<GetMyProposalByJobPostQuery>
{
    public GetMyProposalByJobPostQueryValidator()
    {
        RuleFor(x => x.JobPostId)
            .NotEmpty()
            .WithMessage("JobPostId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
