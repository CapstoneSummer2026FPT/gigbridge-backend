using Application.Common.Models;
using Application.Features.Proposals.Common.DTOs;
using MediatR;
using System;

namespace Application.Features.Proposals.Freelancer.GetMyProposals.Queries;

public class GetMyProposalsQuery : IRequest<PaginatedList<ProposalDto>>
{
    public Guid UserId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int? Status { get; set; }
}