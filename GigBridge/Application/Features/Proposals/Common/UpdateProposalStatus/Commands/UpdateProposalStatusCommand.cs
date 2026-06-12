using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using MediatR;

namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands;

public record UpdateProposalStatusCommand(
    Guid ProposalId,
    Guid UserId,
    UpdateProposalStatusRequest Request
) : IRequest<bool>;