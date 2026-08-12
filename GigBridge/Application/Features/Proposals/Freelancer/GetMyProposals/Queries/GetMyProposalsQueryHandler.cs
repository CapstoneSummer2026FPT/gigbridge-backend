using Domain.Enums.AiInterviews;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.GetMyProposals.Queries;

public class GetMyProposalsQueryHandler : IRequestHandler<GetMyProposalsQuery, PaginatedList<ProposalDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyProposalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ProposalDto>> Handle(GetMyProposalsQuery request, CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var queryable = _context.Set<Proposal>()
            .AsNoTracking()
            .Include(proposal => proposal.JobPosts)
            .Include(proposal => proposal.ProposalWorkBreakdownItems)
            .Include(proposal => proposal.ProposalMilestonePlans)
            .Where(proposal => proposal.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId);

        if (request.Status.HasValue)
        {
            queryable = queryable.Where(proposal => proposal.Status == request.Status.Value);
        }

        var totalCount = await queryable.CountAsync(cancellationToken);

        var proposals = await queryable
            .OrderByDescending(proposal => proposal.SubmittedAt)
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .ToListAsync(cancellationToken);

        var proposalDtos = ProposalProjection.ToDtos(proposals);
        var jobPostIds = proposals.Select(proposal => proposal.JobPostsId).Distinct().ToList();
        if (jobPostIds.Count == 0)
        {
            return new PaginatedList<ProposalDto>(proposalDtos.ToList(), totalCount, request.PageIndex, request.PageSize);
        }

        var definitions = await _context.Set<AiInterviewDefinition>()
            .AsNoTracking()
            .Where(definition => jobPostIds.Contains(definition.JobPostId) &&
                definition.Status != AiInterviewDefinitionStatus.Closed)
            .OrderByDescending(definition => definition.CreatedAt)
            .Select(definition => new
            {
                definition.AiInterviewDefinitionsId,
                definition.JobPostId
            })
            .ToListAsync(cancellationToken);
        var latestDefinitionByJob = definitions
            .GroupBy(definition => definition.JobPostId)
            .ToDictionary(group => group.Key, group => group.First());
        var definitionIds = latestDefinitionByJob.Values
            .Select(definition => definition.AiInterviewDefinitionsId)
            .ToList();
        var attempts = definitionIds.Count == 0
            ? []
            : await _context.Set<AiInterviewAttempt>()
                .AsNoTracking()
                .Where(attempt => definitionIds.Contains(attempt.AiInterviewDefinitionId) &&
                    attempt.FreelancerUserId == request.UserId)
                .Select(attempt => new
                {
                    attempt.AiInterviewDefinitionId,
                    attempt.Status
                })
                .ToListAsync(cancellationToken);

        foreach (var proposalDto in proposalDtos)
        {
            if (!latestDefinitionByJob.TryGetValue(proposalDto.JobPostsId, out var definition)) continue;
            var matchingAttempts = attempts
                .Where(attempt => attempt.AiInterviewDefinitionId == definition.AiInterviewDefinitionsId)
                .ToList();
            proposalDto.HasAiInterview = true;
            proposalDto.AiInterviewDefinitionId = definition.AiInterviewDefinitionsId;
            proposalDto.AiInterviewCompleted = matchingAttempts.Any(
                attempt => attempt.Status == AiInterviewAttemptStatus.Completed);
            proposalDto.AiInterviewInProgress = !proposalDto.AiInterviewCompleted && matchingAttempts.Any(
                attempt => attempt.Status == AiInterviewAttemptStatus.InProgress);
        }

        return new PaginatedList<ProposalDto>(proposalDtos.ToList(), totalCount, request.PageIndex, request.PageSize);
    }

    private static int NormalizePageIndex(int pageIndex)
    {
        return pageIndex < 1 ? 1 : pageIndex;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize is < 1 or > 100 ? 10 : pageSize;
    }
}
