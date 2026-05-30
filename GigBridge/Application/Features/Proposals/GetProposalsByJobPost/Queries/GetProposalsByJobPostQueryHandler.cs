using Application.Features.Proposals.DTOs;
using Application.Features.Proposals.Services;
using MediatR;

namespace Application.Features.Proposals.GetProposalsByJobPost.Queries;

public class GetProposalsByJobPostQueryHandler
    : IRequestHandler<GetProposalsByJobPostQuery, IEnumerable<ProposalDto>>
{
    private readonly IProposalsService _proposalsService;

    public GetProposalsByJobPostQueryHandler(IProposalsService proposalsService)
    {
        _proposalsService = proposalsService;
    }

    public async Task<IEnumerable<ProposalDto>> Handle(
        GetProposalsByJobPostQuery request,
        CancellationToken cancellationToken)
    {
        return await _proposalsService.GetProposalsByJobPostAsync(request, cancellationToken);
    }
}