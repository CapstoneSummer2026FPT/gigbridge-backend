using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.Cheating.Commands;

public record LogProposalCheatingEventCommand(
    Guid ProposalId,
    Guid FreelancerUserId,
    LogProposalCheatingEventRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<CheatingEventLogResponse>;
