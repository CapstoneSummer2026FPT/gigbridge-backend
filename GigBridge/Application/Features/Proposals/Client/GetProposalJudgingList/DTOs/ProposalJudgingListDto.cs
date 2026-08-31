using System;
using System.Collections.Generic;
using Application.Features.Proposals.Common.DTOs;

namespace Application.Features.Proposals.Client.GetProposalJudgingList.DTOs;

public class ProposalJudgingListDto
{
    public Guid JobPostId { get; set; }
    public string JobPostTitle { get; set; } = string.Empty;
    public int TotalProposalsCount { get; set; }
    public int JudgedCount { get; set; }
    public int UnjudgedCount { get; set; }
    public double AverageScore { get; set; }
    public int TopScore { get; set; }
    public int RecommendedCount { get; set; }
    public List<ProposalDto> RankedProposals { get; set; } = new();
}
