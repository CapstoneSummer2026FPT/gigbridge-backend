using System;
using Application.Features.Proposals.Client.GetProposalJudgingList.DTOs;
using MediatR;

namespace Application.Features.Proposals.Client.GetProposalJudgingList.Queries;

public class GetProposalJudgingListQuery : IRequest<ProposalJudgingListDto>
{
    public Guid JobPostId { get; set; }
    public Guid UserId { get; set; }
    public bool? RecommendedOnly { get; set; }
    public int? MinScore { get; set; }
    public string? SortBy { get; set; } = "aiScore";
}
