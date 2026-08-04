using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Freelancer.GetMyCompletedProjects.DTOs;
using Application.Features.JobPosts.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Freelancer.GetMyCompletedProjects.Queries;

public class GetMyCompletedProjectsQueryHandler
    : IRequestHandler<GetMyCompletedProjectsQuery, List<FreelancerCompletedProjectResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetMyCompletedProjectsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FreelancerCompletedProjectResponse>> Handle(
        GetMyCompletedProjectsQuery request,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new ForbiddenAccessException("Only freelancers can view completed projects.");
        }

        var contracts = await _context.Set<Contract>()
            .AsNoTracking()
            .Include(contract => contract.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile!.User)
            .Include(contract => contract.JobPosts)
                .ThenInclude(jobPost => jobPost.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile!.User)
                .ThenInclude(user => user!.UserEloScore)
            .Include(contract => contract.JobPosts.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Include(contract => contract.JobPosts.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Major)
            .Include(contract => contract.JobPosts.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Category)
            .Include(contract => contract.JobPosts.JobPostAttachments)
            .Include(contract => contract.JobPosts.JobPostMilestonePlans)
                .ThenInclude(plan => plan.WorkItems)
            .Where(contract =>
                contract.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId &&
                contract.Status == (int)ContractStatus.Completed)
            .OrderByDescending(contract => contract.CompletedAt ?? contract.CreatedAt)
            .ToListAsync(cancellationToken);

        if (contracts.Count == 0)
        {
            return new List<FreelancerCompletedProjectResponse>();
        }

        var contractIds = contracts.Select(contract => contract.ContractsId).ToList();
        var jobPostIds = contracts.Select(contract => contract.JobPostsId).ToList();

        var reviewedContractIds = (await _context.Set<Review>()
            .AsNoTracking()
            .Where(review =>
                contractIds.Contains(review.ContractsId) &&
                review.ReviewerId == request.UserId)
            .Select(review => review.ContractsId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var aiInterviewJobIds = (await _context.Set<AiInterviewDefinition>()
            .AsNoTracking()
            .Where(definition =>
                jobPostIds.Contains(definition.JobPostId) &&
                definition.Status != AiInterviewDefinitionStatus.Closed)
            .Select(definition => definition.JobPostId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return contracts.Select(contract => new FreelancerCompletedProjectResponse
        {
            ContractId = contract.ContractsId,
            JobPostsId = contract.JobPostsId,
            TotalBudget = contract.TotalBudget,
            Status = contract.Status,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            CompletedAt = contract.CompletedAt,
            ClientName = contract.ClientProfiles?.User?.FullName ?? "Client",
            CanReview = !reviewedContractIds.Contains(contract.ContractsId),
            HasReviewedByCurrentUser = reviewedContractIds.Contains(contract.ContractsId),
            JobPost = JobPostDetailProjection.ToDto(
                contract.JobPosts,
                aiInterviewJobIds.Contains(contract.JobPostsId))
        }).ToList();
    }
}
