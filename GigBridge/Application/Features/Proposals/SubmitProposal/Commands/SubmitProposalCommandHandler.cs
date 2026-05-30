using Application.Features.Proposals.Services;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Proposals.SubmitProposal.Commands;

public class SubmitProposalCommandHandler : IRequestHandler<SubmitProposalCommand, Guid>
{
    private readonly IProposalsService _proposalsService;

    public SubmitProposalCommandHandler(IProposalsService proposalsService)
    {
        _proposalsService = proposalsService;
    }

    public async Task<Guid> Handle(SubmitProposalCommand command, CancellationToken cancellationToken)
    {
        return await _proposalsService.SubmitProposalAsync(command, cancellationToken);
    }
}