using Application.Common.InternalServices.Proposals.Models;
using Domain.Entities;

namespace Application.Common.InternalServices.Proposals.Interfaces;
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
