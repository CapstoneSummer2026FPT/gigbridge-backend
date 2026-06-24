using System;
using MediatR;

namespace Application.Features.Proposals.Common.AcceptForNegotiation.Commands;

public record AcceptProposalForNegotiationCommand(Guid ProposalId, Guid UserId) : IRequest<Guid>;
