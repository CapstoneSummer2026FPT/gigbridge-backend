using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Freelancer.InterviewReview.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.InterviewReview.Commands;

public class CompleteInterviewReviewCommandHandler
    : IRequestHandler<CompleteInterviewReviewCommand, InterviewReviewSessionDto>
{
    private readonly IProposalInterviewReviewService _reviewService;

    public CompleteInterviewReviewCommandHandler(IProposalInterviewReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    public Task<InterviewReviewSessionDto> Handle(
        CompleteInterviewReviewCommand command,
        CancellationToken cancellationToken)
    {
        return _reviewService.CompleteReviewAsync(
            command.ProposalId,
            command.UserId,
            cancellationToken);
    }
}
