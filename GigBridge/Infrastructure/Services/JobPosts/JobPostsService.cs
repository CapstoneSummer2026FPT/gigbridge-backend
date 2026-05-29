using Application.Common.Interfaces.IRepository;
using Application.Features.JobPosts.Common.DTOs;
using Application.Features.JobPosts.CreateJobPost.Commands;
using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.GetAvailableJobPosts.Queries;
using Application.Features.JobPosts.GetJobPostDetail.DTOs;
using Application.Features.JobPosts.GetJobPostDetail.Queries;
using Application.Features.JobPosts.GetMyAppliedJobPosts.Queries;
using Application.Features.JobPosts.GetMyJobPosts.Queries;
using Application.Features.JobPosts.Services;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.JobPosts;

public class JobPostsService : IJobPostsService
{
    private readonly IJobPostRepository _jobPostRepository;
    private readonly IUnitOfWork _unitOfWork;

    public JobPostsService(IJobPostRepository jobPostRepository, IUnitOfWork unitOfWork)
    {
        _jobPostRepository = jobPostRepository;
        _unitOfWork = unitOfWork;
    }

    // === Create ===
    public async Task<Guid> CreateJobPostAsync(CreateJobPostCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = command.ClientProfilesId,
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            BudgetType = request.BudgetType,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            Currency = request.Currency ?? "USD",
            EstimatedDuration = request.EstimatedDuration,
            MaxHires = request.MaxHires,
            ExperienceLevelRequired = request.ExperienceLevelRequired,
            LocationType = request.LocationType,
            Location = request.Location,
            Visibility = request.Visibility ?? 0,
            ApplicationDeadline = request.ApplicationDeadline,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        if (request.SkillIds != null && request.SkillIds.Any())
        {
            foreach (var skillId in request.SkillIds)
            {
                jobPost.JobPostSkills.Add(new JobPostSkill
                {
                    JobPostSkillsId = Guid.NewGuid(),
                    JobPostsId = jobPost.JobPostsId,
                    SkillsId = skillId
                });
            }
        }

        _jobPostRepository.Add(jobPost);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return jobPost.JobPostsId;
    }

    // === Get Available Job Posts ===
    public async Task<IEnumerable<JobPostSummaryDto>> GetAvailableJobPostsAsync(GetAvailableJobPostsQuery request, CancellationToken cancellationToken = default)
    {
        var jobPosts = await _jobPostRepository.GetAllPagedAsync(
            pageIndex: request.PageIndex,
            pageSize: request.PageSize,
            filter: j => j.Status == 1 && (j.Visibility == 0 || j.Visibility == null),
            includeProperties: "JobPostSkills.Skill",
            orderBy: j => j.CreatedAt,
            descending: true
        );

        return jobPosts.Select(j => new JobPostSummaryDto(
            JobPostsId: j.JobPostsId,
            Title: j.Title,
            DescriptionPreview: j.Description.Length > 200 ? j.Description.Substring(0, 200) + "..." : j.Description,
            BudgetType: j.BudgetType,
            BudgetMin: j.BudgetMin,
            BudgetMax: j.BudgetMax,
            ExperienceLevelRequired: j.ExperienceLevelRequired,
            LocationType: j.LocationType,
            CreatedAt: j.CreatedAt,
            SkillNames: j.JobPostSkills.Select(js => js.Skills.Name).ToList()
        ));
    }

    // === Get Job Post Detail ===
    public async Task<JobPostDetailDto> GetJobPostDetailAsync(GetJobPostDetailQuery request, CancellationToken cancellationToken = default)
    {
        var jobPost = await _jobPostRepository.GetJobPostWithDetailsAsync(request.JobPostsId);

        if (jobPost == null)
            throw new Exception("Job Post không tồn tại");

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
            ApplicationDeadline: jobPost.ApplicationDeadline,
            CreatedAt: jobPost.CreatedAt,
            Skills: jobPost.JobPostSkills.Select(s => new JobPostSkillDto(s.SkillsId, s.Skills.Name)).ToList(),
            Attachments: jobPost.JobPostAttachments.Select(a => new AttachmentDto(a.JobPostAttachmentsId, a.FileUrl, a.FileName)).ToList()
        );
    }
    // === Client xem JobPosts của mình ===
    public async Task<IEnumerable<JobPostSummaryDto>> GetMyJobPostsAsync(GetMyJobPostsQuery request, CancellationToken cancellationToken = default)
    {
        var jobPosts = await _jobPostRepository.GetAllPagedAsync(
            pageIndex: request.PageIndex,
            pageSize: request.PageSize,
            filter: j => j.ClientProfilesId == request.ClientProfilesId,
            includeProperties: "JobPostSkills.Skill",
            orderBy: j => j.CreatedAt,
            descending: true
        );

        return jobPosts.Select(j => new JobPostSummaryDto(
            JobPostsId: j.JobPostsId,
            Title: j.Title,
            DescriptionPreview: j.Description.Length > 200 ? j.Description.Substring(0, 200) + "..." : j.Description,
            BudgetType: j.BudgetType,
            BudgetMin: j.BudgetMin,
            BudgetMax: j.BudgetMax,
            ExperienceLevelRequired: j.ExperienceLevelRequired,
            LocationType: j.LocationType,
            CreatedAt: j.CreatedAt,
            SkillNames: j.JobPostSkills.Select(js => js.Skills.Name).ToList()
        ));
    }

    // === Freelancer xem các JobPost đã apply ===
    public async Task<IEnumerable<JobPostSummaryDto>> GetMyAppliedJobPostsAsync(GetMyAppliedJobPostsQuery request, CancellationToken cancellationToken = default)
    {
        var jobPosts = await _jobPostRepository.GetAppliedJobPostsByFreelancerAsync(
            freelancerId: request.FreelancerProfilesId,
            pageIndex: request.PageIndex,
            pageSize: request.PageSize
        );

        return jobPosts.Select(j => new JobPostSummaryDto(
            JobPostsId: j.JobPostsId,
            Title: j.Title,
            DescriptionPreview: j.Description.Length > 200 ? j.Description.Substring(0, 200) + "..." : j.Description,
            BudgetType: j.BudgetType,
            BudgetMin: j.BudgetMin,
            BudgetMax: j.BudgetMax,
            ExperienceLevelRequired: j.ExperienceLevelRequired,
            LocationType: j.LocationType,
            CreatedAt: j.CreatedAt,
            SkillNames: j.JobPostSkills.Select(js => js.Skills.Name).ToList()
        ));
    }
}