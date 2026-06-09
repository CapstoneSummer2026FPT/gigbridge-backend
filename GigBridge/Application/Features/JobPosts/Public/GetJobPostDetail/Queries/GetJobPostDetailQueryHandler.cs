using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Public.GetJobPostDetail.Queries;

public class GetJobPostDetailQueryHandler : IRequestHandler<GetJobPostDetailQuery, JobPostDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetJobPostDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<JobPostDetailDto> Handle(GetJobPostDetailQuery request, CancellationToken cancellationToken)
    {
        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Include(jobPost => jobPost.JobPostAttachments)
            .FirstOrDefaultAsync(jobPost =>
                jobPost.JobPostsId == request.JobPostsId &&
                jobPost.Status == 1 &&
                (jobPost.Visibility == null || jobPost.Visibility == 0),
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        return new JobPostDetailDto(
            JobPostsId: jobPost.JobPostsId,
            ClientProfilesId: jobPost.ClientProfilesId,
            Title: jobPost.Title,
            Description: jobPost.Description,
            BudgetType: jobPost.BudgetType,
            BudgetMin: jobPost.BudgetMin,
            BudgetMax: jobPost.BudgetMax,
            Currency: jobPost.Currency,
            EstimatedDuration: jobPost.EstimatedDuration,
            MaxHires: jobPost.MaxHires,
            ExperienceLevelRequired: jobPost.ExperienceLevelRequired,
            LocationType: jobPost.LocationType,
            Location: jobPost.Location,
            EndDate: jobPost.EndDate,
            CreatedAt: jobPost.CreatedAt,
            Skills: jobPost.JobPostSkills
                .Where(jobPostSkill => jobPostSkill.Skills is not null)
                .Select(jobPostSkill => new JobPostSkillDto(jobPostSkill.SkillsId, jobPostSkill.Skills.Name))
                .ToList(),
            Attachments: jobPost.JobPostAttachments
                .Select(attachment => new AttachmentDto(attachment.JobPostAttachmentsId, attachment.FileUrl, attachment.FileName))
                .ToList());
    }
}
