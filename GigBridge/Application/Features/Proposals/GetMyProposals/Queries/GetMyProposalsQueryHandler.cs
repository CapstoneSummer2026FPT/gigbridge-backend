using Application.Features.Proposals.DTOs;
using Application.Features.Proposals.Services;
using MediatR;

namespace Application.Features.Proposals.GetMyProposals.Queries;

public class GetMyProposalsQueryHandler
    : IRequestHandler<GetMyProposalsQuery, IEnumerable<ProposalDto>>
{
    private readonly IProposalsService _proposalsService;

    public GetMyProposalsQueryHandler(IProposalsService proposalsService)
    {
        _proposalsService = proposalsService;
    }

    public async Task<IEnumerable<ProposalDto>> Handle(
        GetMyProposalsQuery request,
        CancellationToken cancellationToken)
    {
        return await _proposalsService.GetMyProposalsAsync(request, cancellationToken);
    }
}