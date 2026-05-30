using Application.Common.Interfaces;
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
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.JobPosts;

public class JobPostsService : IJobPostsService
{
    private readonly IApplicationDbContext _context;

    public JobPostsService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateJobPostAsync(
    CreateJobPostCommand command,
    CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(
                x => x.UserId == command.UserId,
                cancellationToken);

        if (clientProfile == null)
            throw new Exception("Client profile không tồn tại.");

        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfile.ClientProfilesId,

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

        _context.Set<JobPost>().Add(jobPost);
        await _context.SaveChangesAsync(cancellationToken);

        return jobPost.JobPostsId;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> GetAvailableJobPostsAsync(
    GetAvailableJobPostsQuery request,
    CancellationToken cancellationToken = default)
    {
        var query = _context.Set<JobPost>()
            .AsNoTracking()
            .Include(j => j.JobPostSkills)
                .ThenInclude(js => js.Skills)
            .Where(j => j.Status == 1 && (j.Visibility == 0 || j.Visibility == null))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();

            query = query.Where(j =>
                j.Title.ToLower().Contains(keyword) ||
                j.JobPostSkills.Any(js =>
                    js.Skills != null &&
                    js.Skills.Name.ToLower().Contains(keyword)));
        }
        if (request.BudgetType.HasValue)
        {
            query = query.Where(j =>
                j.BudgetType == request.BudgetType.Value);
        }
        if (request.SkillIds != null && request.SkillIds.Any())
        {
            query = query.Where(j =>
                j.JobPostSkills.Any(js =>
                    request.SkillIds.Contains(js.SkillsId)));
        }

        if (request.BudgetMin.HasValue)
        {
            query = query.Where(j =>
                !j.BudgetMax.HasValue ||
                j.BudgetMax >= request.BudgetMin.Value);
        }

        if (request.BudgetMax.HasValue)
        {
            query = query.Where(j =>
                !j.BudgetMin.HasValue ||
                j.BudgetMin <= request.BudgetMax.Value);
        }

        query = request.SortBy?.ToLower() switch
        {
            "budgetmin" => request.SortDesc
                ? query.OrderByDescending(j => j.BudgetMin)
                : query.OrderBy(j => j.BudgetMin),

            "budgetmax" => request.SortDesc
                ? query.OrderByDescending(j => j.BudgetMax)
                : query.OrderBy(j => j.BudgetMax),

            "newest" => query.OrderByDescending(j => j.CreatedAt),

            _ => query.OrderByDescending(j => j.CreatedAt)
        };

        var jobPosts = await query
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return jobPosts.Select(j => new JobPostSummaryDto(
            JobPostsId: j.JobPostsId,
            Title: j.Title,
            DescriptionPreview: string.IsNullOrEmpty(j.Description)
                ? ""
                : j.Description.Length > 200
                    ? j.Description.Substring(0, 200) + "..."
                    : j.Description,
            BudgetType: j.BudgetType,
            BudgetMin: j.BudgetMin,
            BudgetMax: j.BudgetMax,
            ExperienceLevelRequired: j.ExperienceLevelRequired,
            LocationType: j.LocationType,
            CreatedAt: j.CreatedAt,
            SkillNames: j.JobPostSkills?
                .Where(js => js.Skills != null)
                .Select(js => js.Skills.Name)
                .ToList() ?? new List<string>()
        )).ToList();
    }

    public async Task<JobPostDetailDto> GetJobPostDetailAsync(GetJobPostDetailQuery request, CancellationToken cancellationToken = default)
    {
        var jobPost = await _context.Set<JobPost>()
            .Include(x => x.JobPostSkills).ThenInclude(js => js.Skills)
            .Include(x => x.JobPostAttachments)
            .FirstOrDefaultAsync(x => x.JobPostsId == request.JobPostsId, cancellationToken);

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

    public async Task<IEnumerable<JobPostSummaryDto>> GetMyJobPostsAsync(GetMyJobPostsQuery request, CancellationToken cancellationToken = default)
    {
        var clientProfileId = await _context.Set<ClientProfile>()
    .Where(cp => cp.UserId == request.UserId)
    .Select(cp => cp.ClientProfilesId)
    .FirstOrDefaultAsync(cancellationToken);

        var jobPosts = await _context.Set<JobPost>()
            .Include(j => j.JobPostSkills)
                .ThenInclude(js => js.Skills)
            .Where(j => j.ClientProfilesId == clientProfileId)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return MapToSummaryDto(jobPosts);
    }

    public async Task<IEnumerable<JobPostSummaryDto>> GetMyAppliedJobPostsAsync(
    GetMyAppliedJobPostsQuery request,
    CancellationToken cancellationToken = default)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);

        if (freelancerProfile == null)
            throw new Exception("Freelancer profile không tồn tại.");

        var jobPosts = await _context.Set<JobPost>()
            .Include(j => j.JobPostSkills)
                .ThenInclude(js => js.Skills)
            .Where(j => j.Proposals.Any(
                p => p.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId))
            .OrderByDescending(j => j.CreatedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return MapToSummaryDto(jobPosts);
    }

    private static List<JobPostSummaryDto> MapToSummaryDto(IEnumerable<JobPost> jobPosts)
    {
        return jobPosts.Select(j => new JobPostSummaryDto(
            JobPostsId: j.JobPostsId,
            Title: j.Title,
            DescriptionPreview: string.IsNullOrEmpty(j.Description)
                ? ""
                : (j.Description.Length > 200
                    ? j.Description.Substring(0, 200) + "..."
                    : j.Description),

            BudgetType: j.BudgetType,
            BudgetMin: j.BudgetMin,
            BudgetMax: j.BudgetMax,
            ExperienceLevelRequired: j.ExperienceLevelRequired,
            LocationType: j.LocationType,
            CreatedAt: j.CreatedAt,
            SkillNames: j.JobPostSkills?
                .Where(js => js.Skills != null)
                .Select(js => js.Skills.Name)
                .ToList() ?? new List<string>()
        )).ToList();
    }
}