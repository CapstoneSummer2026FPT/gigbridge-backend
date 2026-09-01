using System.Collections.Generic;
using Application.Features.Proposals.Common.DTOs;

namespace Application.Features.Proposals.Client.JudgeAllProposals.DTOs;

public class BatchJudgeResultDto
{
    public int ProcessedCount { get; set; }
    public int RemainingCount { get; set; }
    public bool IsCompleted { get; set; }
    public List<ProposalDto> ProcessedProposals { get; set; } = new();
}
