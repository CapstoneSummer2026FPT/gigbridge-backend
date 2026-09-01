using System;
using Application.Features.Proposals.Client.JudgeAllProposals.DTOs;
using MediatR;

namespace Application.Features.Proposals.Client.JudgeAllProposals;

public class JudgeAllProposalsCommand : IRequest<BatchJudgeResultDto>
{
    public Guid JobPostId { get; set; }
    public Guid UserId { get; set; }
    public int BatchSize { get; set; } = 10;
}
