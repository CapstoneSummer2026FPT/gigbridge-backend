using Application.Features.Proposals.Common.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Features.Proposals.Freelancer.GetMyProposals.Queries;

public class GetMyProposalsQuery : IRequest<IEnumerable<ProposalDto>>
{
    public Guid UserId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}