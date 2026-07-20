using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Freelancer.GetMyAppliedJobPostDetail.Queries;

public sealed class GetMyAppliedJobPostDetailQueryHandler
    : IRequestHandler<GetMyAppliedJobPostDetailQuery, JobPostDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyAppliedJobPostDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<JobPostDetailDto> Handle(
        GetMyAppliedJobPostDetailQuery request,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new ForbiddenAccessException("Only freelancers can view applied job post details.");
        }

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile.User)
                .ThenInclude(user => user.UserEloScore)
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Major)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Category)
            .Include(jobPost => jobPost.JobPostAttachments)
            .Include(jobPost => jobPost.JobPostMilestonePlans)
                .ThenInclude(plan => plan.WorkItems)
            .FirstOrDefaultAsync(jobPost =>
                jobPost.JobPostsId == request.JobPostId &&
                (
                    jobPost.Proposals.Any(proposal =>
                        proposal.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId &&
                        proposal.Status != 0) ||
                    jobPost.JobInvitations.Any(invitation =>
                        invitation.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId &&
                        invitation.Status != (int)JobInvitationStatus.Declined &&
                        invitation.Status != (int)JobInvitationStatus.Expired &&
                        invitation.Status != (int)JobInvitationStatus.Cancelled)
                ),
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to view it.");
        }

        return new JobPostDetailDto(
            JobPostsId: jobPost.JobPostsId,
            ClientProfilesId: jobPost.ClientProfilesId,
            ClientFullName: jobPost.ClientProfiles?.User?.FullName
                            ?? jobPost.ClientProfiles?.CompanyName,
            Title: jobPost.Title,
            Description: jobPost.Description,
            MajorCategoryId: jobPost.MajorCategoryId,
            MajorId: jobPost.MajorCategory?.MajorId,
            MajorName: jobPost.MajorCategory?.Major?.Name,
            CategoryId: jobPost.MajorCategory?.CategoryId,
            CategoryName: jobPost.MajorCategory?.Category?.Name,
            BudgetMin: jobPost.BudgetMin,
            BudgetMax: jobPost.BudgetMax,
            Currency: jobPost.Currency,
            EstimatedDuration: jobPost.EstimatedDuration,
            Location: jobPost.Location,
            Status: jobPost.Status,
            Visibility: jobPost.Visibility,
            EndDate: jobPost.EndDate,
            CreatedAt: jobPost.CreatedAt,
            EloPoints: jobPost.ClientProfiles?.User?.UserEloScore?.CurrentPoints ?? UserEloCalculator.DefaultPoints,
            Skills: jobPost.JobPostSkills
                .Where(jobPostSkill => jobPostSkill.Skills is not null)
                .Select(jobPostSkill => new JobPostSkillDto(jobPostSkill.SkillsId, jobPostSkill.Skills.Name))
                .ToList(),
            CustomSkillNames: jobPost.CustomSkillNames.ToList(),
            Attachments: jobPost.JobPostAttachments
                .Select(attachment => new AttachmentDto(attachment.JobPostAttachmentsId, attachment.FileUrl, attachment.FileName))
                .ToList(),
            MilestonePlans: Application.Features.JobPosts.Common.JobPostPlanProjection.ToDtos(jobPost.JobPostMilestonePlans));
    }
}
