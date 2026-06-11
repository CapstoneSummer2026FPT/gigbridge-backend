using MediatR;

namespace Application.Features.Chat.Negotiations.StartFromProposal.Commands;

public record StartNegotiationFromProposalCommand(
    Guid ProposalId,
    Guid UserId) : IRequest<Guid>;
