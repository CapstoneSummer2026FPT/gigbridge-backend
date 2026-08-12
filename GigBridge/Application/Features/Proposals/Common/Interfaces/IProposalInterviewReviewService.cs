using Application.Features.Proposals.Freelancer.InterviewReview.DTOs;
using Domain.Entities;

namespace Application.Features.Proposals.Common.Interfaces;

public interface IProposalInterviewReviewService
{
    Task<InterviewReviewSessionDto> StartReviewAsync(
        Guid proposalId,
        Guid freelancerUserId,
        CancellationToken cancellationToken);

    Task<InterviewReviewSessionDto> CompleteReviewAsync(
        Guid proposalId,
        Guid freelancerUserId,
        CancellationToken cancellationToken);

    Task CompleteActiveReviewForSubmissionAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken);
}
