using MediatR;

namespace Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;

public record StartNegotiationFromProposalCommand(
    Guid ProposalId,
    Guid UserId) : IRequest<Guid>;
