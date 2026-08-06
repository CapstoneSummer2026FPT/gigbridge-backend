using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.Common;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.GetMyJobPosts.Queries;

public class GetMyJobPostsQueryHandler : IRequestHandler<GetMyJobPostsQuery, IEnumerable<GetMyJobPostDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GetMyJobPostDto>> Handle(GetMyJobPostsQuery request, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);

        var jobPosts = await _context.Set<JobPost>()
            .AsNoTracking()
            .Where(jobPost => jobPost.ClientProfilesId == clientProfile.ClientProfilesId)
            .OrderByDescending(jobPost => jobPost.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(jobPost => new GetMyJobPostDto
            {
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = jobPost.ClientProfilesId,

                Title = jobPost.Title,
                Description = jobPost.Description,

                MajorCategoryId = jobPost.MajorCategoryId,

                MajorId = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.MajorId
                    : null,

                MajorName = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.Major.Name
                    : null,

                CategoryId = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.CategoryId
                    : null,

                CategoryName = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.Category.Name
                    : null,

                Skills = jobPost.JobPostSkills
                    .Select(jobPostSkill => new GetMyJobPostSkillDto
                    {
                        SkillId = jobPostSkill.SkillsId,
                        Name = jobPostSkill.Skills.Name
                    })
                    .ToList(),

                BudgetMin = jobPost.BudgetMin,
                BudgetMax = jobPost.BudgetMax,
                Currency = jobPost.Currency,
                EstimatedDuration = jobPost.EstimatedDuration,
                Location = jobPost.Location,

                Status = jobPost.Status,
                Visibility = jobPost.Visibility,
                EndDate = jobPost.EndDate,
                IsAigenerated = jobPost.IsAigenerated,
                IsFeatured = jobPost.IsFeatured && jobPost.FeaturedUntil > DateTime.UtcNow,
                FeaturedUntil = jobPost.FeaturedUntil,

                CustomSkillNames = jobPost.CustomSkillNames.ToList(),

                CreatedAt = jobPost.CreatedAt,
                UpdatedAt = jobPost.UpdatedAt,

                ProposalCount = jobPost.Proposals.Count(proposal => proposal.Status != 0)
            })
            .ToListAsync(cancellationToken);

        var jobPostIds = jobPosts.Select(x => x.JobPostsId).ToList();
        var activeInterviews = await _context.Set<AiInterviewDefinition>()
            .AsNoTracking()
            .Where(d => jobPostIds.Contains(d.JobPostId) && d.Status != AiInterviewDefinitionStatus.Closed)
            .Select(d => d.JobPostId)
            .ToListAsync(cancellationToken);

        foreach (var jp in jobPosts)
        {
            jp.HasAiInterview = activeInterviews.Contains(jp.JobPostsId);
        }

        await JobPostSetupProgressBuilder.ApplyAsync(_context, jobPosts, cancellationToken);

        return jobPosts;
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
