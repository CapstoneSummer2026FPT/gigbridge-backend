using Application.Common.Interfaces;
using Application.Features.SavedJobs.Freelancer.GetMySavedJobs.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SavedJobs.Freelancer.GetMySavedJobs.Queries;

public class GetMySavedJobsQueryHandler : IRequestHandler<GetMySavedJobsQuery, IEnumerable<SavedJobDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMySavedJobsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SavedJobDto>> Handle(
        GetMySavedJobsQuery request,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);

        var savedJobs = await _context.Set<SavedJob>()
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SavedJobDto
            {
                SavedJobId = x.SavedJobsId,
                JobPostId = x.JobPostsId,

                Title = x.JobPosts.Title,
                Description = x.JobPosts.Description,

                MajorCategoryId = x.JobPosts.MajorCategoryId,

                MajorId = x.JobPosts.MajorCategory != null
                    ? x.JobPosts.MajorCategory.MajorId
                    : null,

                MajorName = x.JobPosts.MajorCategory != null
                    ? x.JobPosts.MajorCategory.Major.Name
                    : null,

                CategoryId = x.JobPosts.MajorCategory != null
                    ? x.JobPosts.MajorCategory.CategoryId
                    : null,

                CategoryName = x.JobPosts.MajorCategory != null
                    ? x.JobPosts.MajorCategory.Category.Name
                    : null,

                Skills = x.JobPosts.JobPostSkills
                    .Select(jobPostSkill => new SavedJobSkillDto
                    {
                        SkillId = jobPostSkill.SkillsId,
                        Name = jobPostSkill.Skills.Name
                    })
                    .ToList(),

                CustomSkillNames = x.JobPosts.CustomSkillNames.ToList(),

                BudgetMin = x.JobPosts.BudgetMin,
                BudgetMax = x.JobPosts.BudgetMax,
                Currency = x.JobPosts.Currency,
                EstimatedDuration = x.JobPosts.EstimatedDuration,

                Status = x.JobPosts.Status,
                Visibility = x.JobPosts.Visibility,

                JobCreatedAt = x.JobPosts.CreatedAt,
                SavedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return savedJobs;
    }
}