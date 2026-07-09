using Application.Features.Proposals.Common.AcceptForNegotiation.Commands;
using MediatR;

namespace Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;

/// <summary>
/// Compatibility command for the existing conversation route. The proposal workflow is the
/// single source of truth for validation, idempotent conversation creation and notifications.
/// </summary>
public sealed class StartNegotiationFromProposalCommandHandler
    : IRequestHandler<StartNegotiationFromProposalCommand, Guid>
{
    private readonly ISender _sender;

    public StartNegotiationFromProposalCommandHandler(ISender sender) => _sender = sender;

    public Task<Guid> Handle(
        StartNegotiationFromProposalCommand command,
        CancellationToken cancellationToken) =>
        _sender.Send(
            new AcceptProposalForNegotiationCommand(command.ProposalId, command.UserId),
            cancellationToken);
}
