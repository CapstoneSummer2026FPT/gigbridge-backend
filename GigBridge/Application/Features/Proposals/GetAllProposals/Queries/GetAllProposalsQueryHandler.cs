using Application.Features.Proposals.DTOs;
using Application.Features.Proposals.Services;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Proposals.GetAllProposals.Queries;

public class GetAllProposalsQueryHandler : IRequestHandler<GetAllProposalsQuery, IEnumerable<ProposalDto>>
{
    private readonly IProposalsService _proposalsService;

    public GetAllProposalsQueryHandler(IProposalsService proposalsService)
    {
        _proposalsService = proposalsService;
    }

    public async Task<IEnumerable<ProposalDto>> Handle(GetAllProposalsQuery request, CancellationToken cancellationToken)
    {
        return await _proposalsService.GetAllProposalsAsync(request, cancellationToken);
    }
}