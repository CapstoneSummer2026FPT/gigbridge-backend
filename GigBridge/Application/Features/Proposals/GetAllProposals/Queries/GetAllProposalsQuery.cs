using Application.Features.Proposals.DTOs;
using MediatR;
using System.Collections.Generic;

namespace Application.Features.Proposals.GetAllProposals.Queries;

public class GetAllProposalsQuery : IRequest<IEnumerable<ProposalDto>>
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}