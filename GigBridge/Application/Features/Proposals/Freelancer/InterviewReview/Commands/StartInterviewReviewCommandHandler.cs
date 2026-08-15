using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Common.InternalServices.Proposals.Models;
using MediatR;

namespace Application.Features.Proposals.Freelancer.InterviewReview.Commands;

public class StartInterviewReviewCommandHandler
    : IRequestHandler<StartInterviewReviewCommand, InterviewReviewSessionDto>
{
    private readonly IProposalInterviewReviewService _reviewService;

    public StartInterviewReviewCommandHandler(IProposalInterviewReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    public Task<InterviewReviewSessionDto> Handle(
        StartInterviewReviewCommand command,
        CancellationToken cancellationToken)
    {
        return _reviewService.StartReviewAsync(
            command.ProposalId,
            command.UserId,
            cancellationToken);
    }
}
