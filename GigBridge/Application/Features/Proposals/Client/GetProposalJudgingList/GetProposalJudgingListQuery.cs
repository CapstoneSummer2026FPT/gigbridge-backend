using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Client.GetProposalJudgingList;

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

public class GetProposalJudgingListQuery : IRequest<ProposalJudgingListDto>
{
    public Guid JobPostId { get; set; }
    public Guid UserId { get; set; }
    public bool? RecommendedOnly { get; set; }
    public int? MinScore { get; set; }
    public string? SortBy { get; set; } = "aiScore";
}

public class GetProposalJudgingListQueryHandler : IRequestHandler<GetProposalJudgingListQuery, ProposalJudgingListDto>
{
    private readonly IApplicationDbContext _context;

    public GetProposalJudgingListQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProposalJudgingListDto> Handle(GetProposalJudgingListQuery request, CancellationToken cancellationToken)
    {
        // 1. Verify Client profile
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.UserId == request.UserId, cancellationToken);

        if (clientProfile == null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        // 2. Fetch Job Post
        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobPostsId == request.JobPostId, cancellationToken);

        if (jobPost == null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not have permission to view judging list for this job.");
        }

        // 3. Fetch all active proposals for this job post
        var proposals = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(p => p.JobPosts)
            .Include(p => p.FreelancerProfiles)
                .ThenInclude(fp => fp.User)
            .Include(p => p.ProposalWorkBreakdownItems)
            .Include(p => p.ProposalMilestonePlans)
            .Include(p => p.ProposalAiJudging)
            .Where(p => p.JobPostsId == request.JobPostId && p.Status != 0)
            .ToListAsync(cancellationToken);

        var dtos = ProposalProjection.ToDtos(proposals);

        // Calculate aggregate statistics
        var judgedList = dtos.Where(p => p.AiScore.HasValue).ToList();
        int totalCount = dtos.Count;
        int judgedCount = judgedList.Count;
        int unjudgedCount = totalCount - judgedCount;
        double avgScore = judgedCount > 0 ? Math.Round(judgedList.Average(p => p.AiScore!.Value), 1) : 0;
        int topScore = judgedCount > 0 ? judgedList.Max(p => p.AiScore!.Value) : 0;
        int recommendedCount = judgedList.Count(p => p.AiRecommendedHire == true);

        // Apply filtering
        var filtered = dtos.AsEnumerable();

        if (request.RecommendedOnly == true)
        {
            filtered = filtered.Where(p => p.AiRecommendedHire == true);
        }

        if (request.MinScore.HasValue)
        {
            filtered = filtered.Where(p => p.AiScore.HasValue && p.AiScore.Value >= request.MinScore.Value);
        }

        // Apply sorting
        var sorted = request.SortBy?.ToLower() switch
        {
            "budget" => filtered.OrderBy(p => p.ProposedBudget),
            "submittedat" => filtered.OrderByDescending(p => p.SubmittedAt),
            _ => filtered.OrderByDescending(p => p.AiScore ?? -1).ThenByDescending(p => p.SubmittedAt)
        };

        return new ProposalJudgingListDto
        {
            JobPostId = request.JobPostId,
            JobPostTitle = jobPost.Title,
            TotalProposalsCount = totalCount,
            JudgedCount = judgedCount,
            UnjudgedCount = unjudgedCount,
            AverageScore = avgScore,
            TopScore = topScore,
            RecommendedCount = recommendedCount,
            RankedProposals = sorted.ToList()
        };
    }
}
