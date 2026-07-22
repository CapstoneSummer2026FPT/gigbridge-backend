using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.JobPosts.GetDetail.Queries;

public sealed record GetAdminJobPostDetailQuery(
    Guid AdminUserId,
    Guid JobPostId) : IRequest<JobPostDetailDto>;

public sealed class GetAdminJobPostDetailQueryHandler :
    IRequestHandler<GetAdminJobPostDetailQuery, JobPostDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminJobPostDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<JobPostDetailDto> Handle(
        GetAdminJobPostDetailQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can access detailed job post information.");
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
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == request.JobPostId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        var hasAiInterview = await _context.Set<AiInterviewDefinition>()
            .AsNoTracking()
            .AnyAsync(definition => definition.JobPostId == jobPost.JobPostsId &&
                definition.Status != AiInterviewDefinitionStatus.Closed,
                cancellationToken);

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
            MilestonePlans: Application.Features.JobPosts.Common.JobPostPlanProjection.ToDtos(jobPost.JobPostMilestonePlans),
            HasAiInterview: hasAiInterview);
    }
}
