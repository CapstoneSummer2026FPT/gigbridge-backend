using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using Domain.Entities;

namespace Application.Common.Interfaces.IService;

public interface IProposalCheatingService
{
    Task<CheatingEventLogResponse> LogEventAsync(
        Guid proposalId,
        Guid freelancerUserId,
        LogProposalCheatingEventRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    Task<CheatingPenaltyResultDto?> ApplySubmissionPenaltyIfNeededAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken);
}
