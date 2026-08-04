using Application.Features.Proposals.Freelancer.QuestionTimers.DTOs;
using Domain.Entities;

namespace Application.Common.Interfaces.IService;

public interface IProposalQuestionTimerService
{
    Task<QuestionTimerStateDto> StartTimerAsync(
        Guid proposalId,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CancellationToken cancellationToken);

    Task<QuestionTimerStateDto> CompleteTimerAsync(
        Guid proposalId,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CompleteQuestionTimerRequest request,
        CancellationToken cancellationToken);

    Task EnsureQuestionCanBeModifiedAsync(
        Proposal proposal,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CancellationToken cancellationToken);

    Task EnsureProposalReadyForSubmissionAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken);
}
