using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.GetMyDraftJobPosts.Queries;

public sealed class GetMyDraftJobPostsQueryHandler
    : IRequestHandler<GetMyDraftJobPostsQuery, IEnumerable<GetMyJobPostDto>>
{
    private const int DraftStatus = 0;

    private readonly IApplicationDbContext _context;

    public GetMyDraftJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GetMyJobPostDto>> Handle(
        GetMyDraftJobPostsQuery request,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        return await _context.Set<JobPost>()
            .AsNoTracking()
            .Where(jobPost =>
                jobPost.ClientProfilesId == clientProfile.ClientProfilesId &&
                jobPost.Status == DraftStatus)
            .OrderByDescending(jobPost => jobPost.UpdatedAt ?? jobPost.CreatedAt)
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
                CustomSkillNames = jobPost.CustomSkillNames.ToList(),
                BudgetMin = jobPost.BudgetMin,
                BudgetMax = jobPost.BudgetMax,
                Currency = jobPost.Currency,
                EstimatedDuration = jobPost.EstimatedDuration,
                Location = jobPost.Location,
                Status = jobPost.Status,
                Visibility = jobPost.Visibility,
                EndDate = jobPost.EndDate,
                IsAigenerated = jobPost.IsAigenerated,
                CreatedAt = jobPost.CreatedAt,
                UpdatedAt = jobPost.UpdatedAt,
                ProposalCount = jobPost.Proposals.Count(proposal => proposal.Status != 0)
            })
            .ToListAsync(cancellationToken);
    }
}
