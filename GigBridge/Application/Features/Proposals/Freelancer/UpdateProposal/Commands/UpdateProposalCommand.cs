using Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.UpdateProposal.Commands;

public record UpdateProposalCommand(
    Guid ProposalId,
    Guid UserId,
    UpdateProposalRequest Request
) : IRequest<bool>;